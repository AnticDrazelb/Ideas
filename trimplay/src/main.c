// TrimPlay: turns a TrimUI Smart Pro into a Remote Play screen + controller
// for a PS4 (and, over H.264, a PS5) on the same network.
#include "app.h"

#include <ctype.h>
#include <stdatomic.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <strings.h>

#include <arpa/inet.h>
#include <netinet/in.h>

#include <chiaki/base64.h>
#include <chiaki/discoveryservice.h>

typedef enum {
	SCR_HOSTS,
	SCR_PIN,          // entering the 8-digit pairing PIN or 4-digit login passcode
	SCR_REGISTERING,
	SCR_WAKING,
	SCR_STREAM,
	SCR_MESSAGE,
	SCR_CONFIRM_FORGET,
	SCR_INPUT_TEST,
} Screen;

typedef enum { PIN_FOR_REGIST, PIN_FOR_LOGIN } PinPurpose;

static struct {
	char base[512];
	char cfg_path[600], hosts_path[600];
	TpConfig cfg;
	TpRegistry reg;
	ChiakiLog log;
	TpUi ui;
	TpInput in;

	// discovery (written on the discovery thread)
	ChiakiDiscoveryService disc;
	bool disc_running;
	SDL_mutex *disc_mutex;
	TpHost found[TP_MAX_HOSTS];
	int found_count;

	// merged list shown on screen
	TpHost hosts[TP_MAX_HOSTS * 2];
	int host_count;
	int sel;

	Screen screen;
	char msg_title[64], msg_body[3][160];
	Screen msg_return;

	// pin entry
	PinPurpose pin_for;
	int pin_len, pin_cur;
	int pin[8];

	// registration
	ChiakiRegist regist;
	ChiakiRegistInfo regist_info;
	bool regist_running;
	atomic_int regist_result;  // 0 pending, 1 ok, 2 failed, 3 canceled
	ChiakiRegisteredHost regist_host;
	TpHost target;             // console being paired / connected

	// waking
	uint32_t wake_deadline, wake_resend;

	// stream
	uint32_t stream_started, menu_down_at, quit_hold_at;
	bool menu_chorded, show_stats;
	bool stream_armed;         // false until the button that started the stream is released
	uint32_t ps_pulse_until;
} A;

// ---------------------------------------------------------------- helpers

static void message(const char *title, const char *l1, const char *l2, const char *l3, Screen ret)
{
	snprintf(A.msg_title, sizeof(A.msg_title), "%s", title);
	snprintf(A.msg_body[0], sizeof(A.msg_body[0]), "%s", l1 ? l1 : "");
	snprintf(A.msg_body[1], sizeof(A.msg_body[1]), "%s", l2 ? l2 : "");
	snprintf(A.msg_body[2], sizeof(A.msg_body[2]), "%s", l3 ? l3 : "");
	A.msg_return = ret;
	A.screen = SCR_MESSAGE;
}

static void log_cb(ChiakiLogLevel level, const char *msg, void *user)
{
	(void)user;
	fprintf(stderr, "[chiaki %c] %s\n", chiaki_log_level_char(level), msg);
}

static void upper(char *s)
{
	for(; *s; s++)
		*s = (char)toupper((unsigned char)*s);
}

// ---------------------------------------------------------------- discovery

static void disc_cb(ChiakiDiscoveryHost *hosts, size_t n, void *user)
{
	(void)user;
	SDL_LockMutex(A.disc_mutex);
	A.found_count = 0;
	for(size_t i = 0; i < n && A.found_count < TP_MAX_HOSTS; i++)
	{
		ChiakiDiscoveryHost *d = &hosts[i];
		TpHost *h = &A.found[A.found_count++];
		memset(h, 0, sizeof(*h));
		snprintf(h->mac, sizeof(h->mac), "%s", d->host_id ? d->host_id : "");
		upper(h->mac);
		snprintf(h->name, sizeof(h->name), "%s", d->host_name ? d->host_name : "PlayStation");
		snprintf(h->addr, sizeof(h->addr), "%s", d->host_addr ? d->host_addr : "");
		snprintf(h->app, sizeof(h->app), "%s", d->running_app_name ? d->running_app_name : "");
		h->state = d->state;
		h->ps5 = chiaki_discovery_host_is_ps5(d);
		h->target = chiaki_discovery_host_system_version_target(d);
		h->seen = true;
	}
	SDL_UnlockMutex(A.disc_mutex);
}

static bool disc_start(void)
{
	ChiakiDiscoveryServiceOptions o;
	memset(&o, 0, sizeof(o));
	o.ping_ms = 500;
	o.ping_initial_ms = 500;
	o.hosts_max = TP_MAX_HOSTS;
	o.host_drop_pings = 3;
	o.cb = disc_cb;

	struct sockaddr_in sin;
	memset(&sin, 0, sizeof(sin));
	sin.sin_family = AF_INET;
	sin.sin_addr.s_addr = 0xffffffff; // broadcast
	if(A.cfg.manual_host[0] && inet_pton(AF_INET, A.cfg.manual_host, &sin.sin_addr) != 1)
		SDL_Log("host=%s is not an IPv4 address, using broadcast", A.cfg.manual_host);
	struct sockaddr_storage ss;
	memset(&ss, 0, sizeof(ss));
	memcpy(&ss, &sin, sizeof(sin));
	o.send_addr = &ss;
	o.send_addr_size = sizeof(sin);

	ChiakiErrorCode err = chiaki_discovery_service_init(&A.disc, &o, &A.log);
	if(err != CHIAKI_ERR_SUCCESS)
	{
		SDL_Log("Discovery failed: %s", chiaki_error_string(err));
		return false;
	}
	A.disc_running = true;
	return true;
}

static void hosts_rebuild(void)
{
	A.host_count = 0;
	SDL_LockMutex(A.disc_mutex);
	for(int i = 0; i < A.found_count; i++)
	{
		TpHost h = A.found[i];
		h.regist_idx = tp_registry_find(&A.reg, h.mac);
		A.hosts[A.host_count++] = h;
	}
	SDL_UnlockMutex(A.disc_mutex);
	// Paired consoles that aren't answering (off, or broadcast is blocked):
	// still listed so you can wake/connect them by their last known IP.
	for(int i = 0; i < A.reg.count; i++)
	{
		bool present = false;
		for(int j = 0; j < A.host_count; j++)
			if(A.hosts[j].regist_idx == i)
				present = true;
		if(present)
			continue;
		TpHost *h = &A.hosts[A.host_count++];
		memset(h, 0, sizeof(*h));
		snprintf(h->mac, sizeof(h->mac), "%s", A.reg.items[i].mac);
		snprintf(h->name, sizeof(h->name), "%s", A.reg.items[i].nickname);
		snprintf(h->addr, sizeof(h->addr), "%s", A.reg.items[i].last_ip[0] ? A.reg.items[i].last_ip : A.cfg.manual_host);
		h->target = A.reg.items[i].target;
		h->ps5 = chiaki_target_is_ps5(h->target);
		h->state = CHIAKI_DISCOVERY_HOST_STATE_UNKNOWN;
		h->regist_idx = i;
	}
	if(A.sel >= A.host_count)
		A.sel = A.host_count ? A.host_count - 1 : 0;
}

static const TpHost *host_by_mac(const char *mac)
{
	for(int i = 0; i < A.host_count; i++)
		if(!strcasecmp(A.hosts[i].mac, mac))
			return &A.hosts[i];
	return NULL;
}

// ---------------------------------------------------------------- pairing

static void regist_cb(ChiakiRegistEvent *ev, void *user)
{
	(void)user;
	if(ev->type == CHIAKI_REGIST_EVENT_TYPE_FINISHED_SUCCESS)
	{
		A.regist_host = *ev->registered_host;
		atomic_store(&A.regist_result, 1);
	}
	else
		atomic_store(&A.regist_result, ev->type == CHIAKI_REGIST_EVENT_TYPE_FINISHED_CANCELED ? 3 : 2);
}

static void regist_begin(uint32_t pin)
{
	memset(&A.regist_info, 0, sizeof(A.regist_info));
	A.regist_info.target = A.target.target;
	A.regist_info.host = A.target.addr;
	A.regist_info.broadcast = false;
	A.regist_info.pin = pin;
	size_t sz = sizeof(A.regist_info.psn_account_id);
	if(chiaki_base64_decode(A.cfg.psn_account_id, strlen(A.cfg.psn_account_id),
			A.regist_info.psn_account_id, &sz) != CHIAKI_ERR_SUCCESS || sz != CHIAKI_PSN_ACCOUNT_ID_SIZE)
	{
		message("Pairing", "psn_account_id in trimplay.ini isn't valid.",
			"It should be 12 characters of base64, ending in '='.", "See README: Getting your account ID.", SCR_HOSTS);
		return;
	}
	atomic_store(&A.regist_result, 0);
	ChiakiErrorCode err = chiaki_regist_start(&A.regist, &A.log, &A.regist_info, regist_cb, NULL);
	if(err != CHIAKI_ERR_SUCCESS)
	{
		message("Pairing", "Could not start pairing:", chiaki_error_string(err), NULL, SCR_HOSTS);
		return;
	}
	A.regist_running = true;
	A.screen = SCR_REGISTERING;
}

static void regist_finish(void)
{
	chiaki_regist_stop(&A.regist);
	chiaki_regist_fini(&A.regist);
	A.regist_running = false;
}

// ---------------------------------------------------------------- stream

static void start_stream(const TpHost *h)
{
	TpRegistered *r = &A.reg.items[h->regist_idx];
	if(h->addr[0] && strcmp(r->last_ip, h->addr))
	{
		snprintf(r->last_ip, sizeof(r->last_ip), "%s", h->addr);
		tp_registry_save(&A.reg, A.hosts_path);
	}
	if(!tp_stream_start(&A.cfg, &A.log, r, h->addr, h->ps5, &A.ui))
	{
		message("Couldn't connect", tp_stream_end_reason(), NULL, NULL, SCR_HOSTS);
		tp_stream_stop();
		return;
	}
	A.stream_started = SDL_GetTicks();
	A.menu_down_at = 0;
	A.quit_hold_at = 0;
	A.ps_pulse_until = 0;
	A.show_stats = A.cfg.show_stats;
	A.stream_armed = false;
	A.screen = SCR_STREAM;
}

static bool send_wakeup(const TpHost *h)
{
	const TpRegistered *r = &A.reg.items[h->regist_idx];
	char key[CHIAKI_SESSION_AUTH_SIZE + 1];
	memcpy(key, r->regist_key, CHIAKI_SESSION_AUTH_SIZE);
	key[CHIAKI_SESSION_AUTH_SIZE] = '\0';
	uint64_t credential = strtoull(key, NULL, 16);
	return chiaki_discovery_wakeup(&A.log, NULL,
		h->addr, credential, h->ps5) == CHIAKI_ERR_SUCCESS;
}

// Builds the DualShock state from the Smart Pro's controls. MENU is the
// "function" key: tap = PS, hold alone = PS held, MENU+x = shortcuts.
static bool stream_controls(ChiakiControllerState *st)
{
	TpInput *in = &A.in;
	uint32_t now = SDL_GetTicks();
	chiaki_controller_state_set_idle(st);

	bool menu = in->down[TB_MENU];
	if(in->pressed[TB_MENU])
	{
		A.menu_down_at = now;
		A.menu_chorded = false;
	}

	if(menu)
	{
		if(in->down[TB_START])
		{
			A.menu_chorded = true;
			if(!A.quit_hold_at)
				A.quit_hold_at = now;
			if(now - A.quit_hold_at > 1000)
				return false; // disconnect
		}
		else
			A.quit_hold_at = 0;
		if(in->down[TB_SELECT])
		{
			A.menu_chorded = true;
			st->buttons |= CHIAKI_CONTROLLER_BUTTON_TOUCHPAD;
		}
		if(in->pressed[TB_L1])
		{
			A.menu_chorded = true;
			A.show_stats = !A.show_stats;
		}
		if(!A.menu_chorded && now - A.menu_down_at > 400)
			st->buttons |= CHIAKI_CONTROLLER_BUTTON_PS;
		return true; // everything else is suppressed while MENU is held
	}
	if(A.menu_down_at)
	{
		// MENU just released: a short tap with no shortcut is a PS press.
		if(!A.menu_chorded && now - A.menu_down_at <= 400)
			A.ps_pulse_until = now + 120;
		A.menu_down_at = 0;
		A.quit_hold_at = 0;
	}
	if(now < A.ps_pulse_until)
		st->buttons |= CHIAKI_CONTROLLER_BUTTON_PS;

	// Positional mapping: bottom face button is Cross, right is Circle, etc.
	TpButton cross = A.cfg.swap_ab ? TB_EAST : TB_SOUTH;
	TpButton moon = A.cfg.swap_ab ? TB_SOUTH : TB_EAST;
	if(in->down[cross]) st->buttons |= CHIAKI_CONTROLLER_BUTTON_CROSS;
	if(in->down[moon]) st->buttons |= CHIAKI_CONTROLLER_BUTTON_MOON;
	if(in->down[TB_WEST]) st->buttons |= CHIAKI_CONTROLLER_BUTTON_BOX;
	if(in->down[TB_NORTH]) st->buttons |= CHIAKI_CONTROLLER_BUTTON_PYRAMID;
	if(in->down[TB_UP]) st->buttons |= CHIAKI_CONTROLLER_BUTTON_DPAD_UP;
	if(in->down[TB_DOWN]) st->buttons |= CHIAKI_CONTROLLER_BUTTON_DPAD_DOWN;
	if(in->down[TB_LEFT]) st->buttons |= CHIAKI_CONTROLLER_BUTTON_DPAD_LEFT;
	if(in->down[TB_RIGHT]) st->buttons |= CHIAKI_CONTROLLER_BUTTON_DPAD_RIGHT;
	if(in->down[TB_L1]) st->buttons |= CHIAKI_CONTROLLER_BUTTON_L1;
	if(in->down[TB_R1]) st->buttons |= CHIAKI_CONTROLLER_BUTTON_R1;
	if(in->down[TB_L3]) st->buttons |= CHIAKI_CONTROLLER_BUTTON_L3;
	if(in->down[TB_R3]) st->buttons |= CHIAKI_CONTROLLER_BUTTON_R3;
	if(in->down[TB_START]) st->buttons |= CHIAKI_CONTROLLER_BUTTON_OPTIONS;
	if(in->down[TB_SELECT]) st->buttons |= CHIAKI_CONTROLLER_BUTTON_SHARE;
	st->l2_state = in->down[TB_L2] ? 0xff : 0;
	st->r2_state = in->down[TB_R2] ? 0xff : 0;
	st->left_x = in->lx;
	st->left_y = in->ly;
	st->right_x = in->rx;
	st->right_y = in->ry;
	return true;
}

// ---------------------------------------------------------------- screens

static const char *state_label(const TpHost *h, SDL_Color *c)
{
	if(!h->seen) { *c = TP_DIM; return "not found"; }
	switch(h->state)
	{
		case CHIAKI_DISCOVERY_HOST_STATE_READY: *c = TP_GOOD; return "ready";
		case CHIAKI_DISCOVERY_HOST_STATE_STANDBY: *c = TP_WARN; return "rest mode";
		default: *c = TP_DIM; return "unknown";
	}
}

static void footer(const char *s)
{
	tp_ui_rect(&A.ui, 0, A.ui.h - 56, A.ui.w, 56, TP_PANEL);
	tp_ui_text(&A.ui, A.ui.font_small, 32, A.ui.h - 42, TP_DIM, "%s", s);
}

static void draw_hosts(void)
{
	TpUi *ui = &A.ui;
	tp_ui_text(ui, ui->font_big, 48, 32, TP_WHITE, "TrimPlay");
	tp_ui_text(ui, ui->font_small, 52, 32 + ui->h / 12 + 4, TP_DIM, "PlayStation Remote Play  -  %dp / %d fps",
		A.cfg.resolution, A.cfg.fps);

	int y = 170, row = 92;
	if(!A.host_count)
	{
		tp_ui_text_center(ui, ui->font, 260, TP_WHITE, "Looking for consoles on your Wi-Fi...");
		tp_ui_text_center(ui, ui->font_small, 310, TP_DIM, "Turn the PS4 on (or put it in rest mode) on the same network.");
		tp_ui_text_center(ui, ui->font_small, 340, TP_DIM, "Still nothing? Set host=<console IP> in trimplay.ini.");
	}
	for(int i = 0; i < A.host_count; i++)
	{
		const TpHost *h = &A.hosts[i];
		bool sel = i == A.sel;
		tp_ui_rect(ui, 40, y, ui->w - 80, row - 12, sel ? (SDL_Color){38, 58, 100, 255} : TP_PANEL);
		if(sel)
			tp_ui_rect(ui, 40, y, 6, row - 12, TP_ACCENT);
		tp_ui_text(ui, ui->font, 70, y + 10, TP_WHITE, "%s", h->name[0] ? h->name : "PlayStation");
		SDL_Color sc;
		const char *sl = state_label(h, &sc);
		tp_ui_text(ui, ui->font_small, 70, y + 48, sc, "%s", sl);
		tp_ui_text(ui, ui->font_small, 220, y + 48, TP_DIM, "%s%s%s", h->addr, h->app[0] ? "  -  " : "", h->app);
		tp_ui_text(ui, ui->font_small, ui->w - 230, y + 22, h->regist_idx >= 0 ? TP_GOOD : TP_WARN, "%s",
			h->regist_idx >= 0 ? "paired" : "not paired");
		y += row;
		if(y > ui->h - 150)
			break;
	}
	footer("A connect    X pair    Y wake    SELECT forget    START button test    MENU quit");
}

static void draw_pin(void)
{
	TpUi *ui = &A.ui;
	if(A.pin_for == PIN_FOR_LOGIN)
	{
		tp_stream_render(ui, false);
		tp_ui_rect(ui, 0, 0, ui->w, ui->h, (SDL_Color){0, 0, 0, 190});
	}
	tp_ui_text_center(ui, ui->font_big, 110, TP_WHITE, A.pin_for == PIN_FOR_REGIST ? "Pair with %s" : "Console passcode",
		A.target.name);
	if(A.pin_for == PIN_FOR_REGIST)
	{
		tp_ui_text_center(ui, ui->font_small, 200, TP_DIM, "On the PS4: Settings > Remote Play Connection Settings > Add Device");
		tp_ui_text_center(ui, ui->font_small, 230, TP_DIM, "then enter the 8-digit number it shows.");
	}
	else
		tp_ui_text_center(ui, ui->font_small, 215, TP_DIM, "Enter the 4-digit login passcode for your PS4 user.");

	int cw = 90, gap = 14, total = A.pin_len * cw + (A.pin_len - 1) * gap;
	int x = (ui->w - total) / 2, y = 300;
	for(int i = 0; i < A.pin_len; i++)
	{
		bool cur = i == A.pin_cur;
		tp_ui_rect(ui, x, y, cw, 120, cur ? (SDL_Color){38, 58, 100, 255} : TP_PANEL);
		if(cur)
			tp_ui_rect(ui, x, y + 114, cw, 6, TP_ACCENT);
		char d[2] = {(char)('0' + A.pin[i]), 0};
		TTF_Font *f = ui->font_big;
		int tw = 0;
		TTF_SizeUTF8(f, d, &tw, NULL);
		tp_ui_text(ui, f, x + (cw - tw) / 2, y + 22, TP_WHITE, "%s", d);
		x += cw + gap;
	}
	footer("UP/DOWN change digit    LEFT/RIGHT move    A done    B cancel");
}

static void draw_center_status(const char *title, const char *sub)
{
	tp_ui_text_center(&A.ui, A.ui.font_big, A.ui.h / 2 - 70, TP_WHITE, "%s", title);
	tp_ui_text_center(&A.ui, A.ui.font_small, A.ui.h / 2 + 10, TP_DIM, "%s", sub);
	int dots = (int)(SDL_GetTicks() / 300 % 4);
	tp_ui_text_center(&A.ui, A.ui.font, A.ui.h / 2 + 50, TP_ACCENT, "%.*s", dots, "...");
}

static void draw_message(void)
{
	tp_ui_text_center(&A.ui, A.ui.font_big, 180, TP_WHITE, "%s", A.msg_title);
	for(int i = 0; i < 3; i++)
		tp_ui_text_center(&A.ui, A.ui.font, 300 + i * 46, i ? TP_DIM : TP_WHITE, "%s", A.msg_body[i]);
	footer("A ok");
}

static void draw_input_test(void)
{
	TpUi *ui = &A.ui;
	tp_ui_text(ui, ui->font_big, 48, 32, TP_WHITE, "Button test");
	tp_ui_text(ui, ui->font_small, 52, 110, TP_DIM,
		"Press each button. Lit = TrimPlay understands it. Use the raw numbers for [joystick]/[keys] in trimplay.ini.");
	int x = 60, y = 170;
	for(int b = 0; b < TB_COUNT; b++)
	{
		bool on = A.in.down[b];
		tp_ui_rect(ui, x, y, 220, 52, on ? (SDL_Color){38, 120, 70, 255} : TP_PANEL);
		tp_ui_text(ui, ui->font_small, x + 16, y + 12, on ? TP_WHITE : TP_DIM, "%s", tp_button_names[b]);
		x += 236;
		if(x + 220 > ui->w - 40) { x = 60; y += 64; }
	}
	y += 90;
	tp_ui_text(ui, ui->font_small, 60, y, TP_WHITE, "left stick %6d %6d    right stick %6d %6d",
		A.in.lx, A.in.ly, A.in.rx, A.in.ry);
	tp_ui_text(ui, ui->font, 60, y + 46, TP_ACCENT, "last raw: %s", A.in.last_raw[0] ? A.in.last_raw : "-");
	footer("Hold START + SELECT to leave (or wait 20 s without pressing anything)");
}

// ---------------------------------------------------------------- input per screen

static void open_pin(PinPurpose p)
{
	A.pin_for = p;
	A.pin_len = p == PIN_FOR_REGIST ? 8 : 4;
	A.pin_cur = 0;
	memset(A.pin, 0, sizeof(A.pin));
	A.screen = SCR_PIN;
}

static void update_hosts(void)
{
	TpInput *in = &A.in;
	if(in->pressed[TB_UP] && A.sel > 0) A.sel--;
	if(in->pressed[TB_DOWN] && A.sel < A.host_count - 1) A.sel++;
	if(in->pressed[TB_MENU]) { in->quit = true; return; }
	if(in->pressed[TB_START]) { A.screen = SCR_INPUT_TEST; A.in.last_raw_ms = SDL_GetTicks(); return; }
	if(!A.host_count)
		return;
	const TpHost *h = &A.hosts[A.sel];
	A.target = *h;

	if(in->pressed[TB_NORTH]) // X: pair
	{
		if(!h->seen || h->state != CHIAKI_DISCOVERY_HOST_STATE_READY)
			message("Pairing", "The console has to be fully on (not in rest mode)", "and visible on the network to pair.", NULL, SCR_HOSTS);
		else if(!A.cfg.psn_account_id[0])
			message("Pairing", "Add your psn_account_id to trimplay.ini first.", "See README: Getting your account ID.", NULL, SCR_HOSTS);
		else
			open_pin(PIN_FOR_REGIST);
	}
	else if(in->pressed[TB_WEST]) // Y: wake
	{
		if(h->regist_idx < 0)
			message("Wake", "Pair with this console first.", NULL, NULL, SCR_HOSTS);
		else if(send_wakeup(h))
			message("Wake", "Wake-up sent.", "The console takes ~20 s to come out of rest mode.", NULL, SCR_HOSTS);
		else
			message("Wake", "Couldn't send the wake-up packet.", NULL, NULL, SCR_HOSTS);
	}
	else if(in->pressed[TB_SELECT] && h->regist_idx >= 0)
		A.screen = SCR_CONFIRM_FORGET;
	else if(in->pressed[TB_EAST]) // A: connect
	{
		if(h->regist_idx < 0)
			message("Not paired yet", "Press X to pair this device with the console.", NULL, NULL, SCR_HOSTS);
		else if(h->seen && h->state == CHIAKI_DISCOVERY_HOST_STATE_READY)
			start_stream(h);
		else if(!h->addr[0])
			message("Connect", "No address known for this console.", "Set host=<console IP> in trimplay.ini.", NULL, SCR_HOSTS);
		else
		{
			send_wakeup(h);
			uint32_t now = SDL_GetTicks();
			A.wake_deadline = now + 45000;
			A.wake_resend = now + 5000;
			A.screen = SCR_WAKING;
		}
	}
}

static void update_pin(void)
{
	TpInput *in = &A.in;
	if(in->pressed[TB_UP]) A.pin[A.pin_cur] = (A.pin[A.pin_cur] + 1) % 10;
	if(in->pressed[TB_DOWN]) A.pin[A.pin_cur] = (A.pin[A.pin_cur] + 9) % 10;
	if(in->pressed[TB_LEFT] && A.pin_cur > 0) A.pin_cur--;
	if(in->pressed[TB_RIGHT] && A.pin_cur < A.pin_len - 1) A.pin_cur++;
	if(in->pressed[TB_SOUTH])
	{
		if(A.pin_for == PIN_FOR_LOGIN)
		{
			tp_stream_stop();
			message("Disconnected", "Passcode entry cancelled.", NULL, NULL, SCR_HOSTS);
		}
		else
			A.screen = SCR_HOSTS;
		return;
	}
	if(in->pressed[TB_EAST])
	{
		char s[9] = {0};
		for(int i = 0; i < A.pin_len; i++)
			s[i] = (char)('0' + A.pin[i]);
		if(A.pin_for == PIN_FOR_LOGIN)
		{
			tp_stream_login_pin(s);
			A.stream_armed = false;
			A.screen = SCR_STREAM;
		}
		else
			regist_begin((uint32_t)strtoul(s, NULL, 10));
	}
}

static void update_registering(void)
{
	if(A.in.pressed[TB_SOUTH])
	{
		regist_finish();
		A.screen = SCR_HOSTS;
		return;
	}
	int r = atomic_load(&A.regist_result);
	if(!r)
		return;
	regist_finish();
	if(r != 1)
	{
		message("Pairing failed", "Check the PIN (it changes every few minutes),",
			"that psn_account_id belongs to the PS4's user,", "and that Remote Play is enabled on the PS4.", SCR_HOSTS);
		return;
	}
	TpRegistered nr;
	memset(&nr, 0, sizeof(nr));
	char mac[13];
	tp_hex_encode(A.regist_host.server_mac, 6, mac);
	upper(mac);
	snprintf(nr.mac, sizeof(nr.mac), "%s", mac);
	snprintf(nr.nickname, sizeof(nr.nickname), "%s", A.regist_host.server_nickname[0] ? A.regist_host.server_nickname : A.target.name);
	nr.target = A.regist_host.target ? A.regist_host.target : A.target.target;
	memcpy(nr.regist_key, A.regist_host.rp_regist_key, sizeof(nr.regist_key));
	memcpy(nr.rp_key, A.regist_host.rp_key, sizeof(nr.rp_key));
	snprintf(nr.last_ip, sizeof(nr.last_ip), "%s", A.target.addr);
	if(tp_registry_put(&A.reg, &nr) < 0 || !tp_registry_save(&A.reg, A.hosts_path))
		message("Paired, but...", "Couldn't save hosts.ini - is the SD card read-only?", NULL, NULL, SCR_HOSTS);
	else
		message("Paired!", "Press A on the console to start playing.", NULL, NULL, SCR_HOSTS);
}

static void update_waking(void)
{
	if(A.in.pressed[TB_SOUTH]) { A.screen = SCR_HOSTS; return; }
	uint32_t now = SDL_GetTicks();
	const TpHost *h = host_by_mac(A.target.mac);
	if(h && h->seen && h->state == CHIAKI_DISCOVERY_HOST_STATE_READY)
	{
		start_stream(h);
		return;
	}
	if(now > A.wake_resend && h && h->regist_idx >= 0)
	{
		send_wakeup(h);
		A.wake_resend = now + 5000;
	}
	if(now > A.wake_deadline)
		message("Console didn't wake", "Make sure it's in rest mode with", "'Stay connected to the Internet' enabled", "under Power Saving Settings.", SCR_HOSTS);
}

static void update_stream(void)
{
	TpStreamState s = tp_stream_state();
	if(s == TS_ENDED)
	{
		char why[160];
		snprintf(why, sizeof(why), "%s", tp_stream_end_reason());
		tp_stream_stop();
		message("Disconnected", why, NULL, NULL, SCR_HOSTS);
		return;
	}
	if(s == TS_LOGIN_PIN)
	{
		open_pin(PIN_FOR_LOGIN);
		return;
	}
	ChiakiControllerState st;
	if(!A.stream_armed)
	{
		// Don't forward the press that got us here (A would arrive as Circle).
		bool any = false;
		for(int b = 0; b < TB_COUNT; b++)
			any |= A.in.down[b];
		A.stream_armed = !any;
		chiaki_controller_state_set_idle(&st);
		tp_stream_set_controller(&st);
		return;
	}
	if(!stream_controls(&st))
	{
		tp_stream_stop();
		A.screen = SCR_HOSTS;
		return;
	}
	tp_stream_set_controller(&st);
}

static void update_input_test(void)
{
	uint32_t now = SDL_GetTicks();
	bool any = false;
	for(int b = 0; b < TB_COUNT; b++)
		any |= A.in.down[b];
	if(any)
		A.in.last_raw_ms = now;
	if((A.in.down[TB_START] && A.in.down[TB_SELECT]) || now - A.in.last_raw_ms > 20000)
		A.screen = SCR_HOSTS;
}

// ---------------------------------------------------------------- main

int main(int argc, char **argv)
{
	(void)argc;
	(void)argv;
	memset(&A, 0, sizeof(A));
	char *bp = SDL_GetBasePath();
	snprintf(A.base, sizeof(A.base), "%s", bp ? bp : "./");
	SDL_free(bp);
	const char *dir = getenv("TRIMPLAY_HOME") ? getenv("TRIMPLAY_HOME") : A.base;
	snprintf(A.cfg_path, sizeof(A.cfg_path), "%s/trimplay.ini", dir);
	snprintf(A.hosts_path, sizeof(A.hosts_path), "%s/hosts.ini", dir);

	tp_config_defaults(&A.cfg);
	if(!tp_config_load(&A.cfg, A.cfg_path))
		fprintf(stderr, "No %s, using defaults\n", A.cfg_path);
	tp_registry_load(&A.reg, A.hosts_path);

	chiaki_log_init(&A.log, A.cfg.verbose_log ? CHIAKI_LOG_ALL : (CHIAKI_LOG_ALL & ~(CHIAKI_LOG_VERBOSE | CHIAKI_LOG_DEBUG)), log_cb, NULL);
	ChiakiErrorCode err = chiaki_lib_init();
	if(err != CHIAKI_ERR_SUCCESS)
	{
		fprintf(stderr, "chiaki_lib_init: %s\n", chiaki_error_string(err));
		return 1;
	}

	if(SDL_Init(SDL_INIT_VIDEO | SDL_INIT_AUDIO | SDL_INIT_GAMECONTROLLER | SDL_INIT_JOYSTICK) != 0)
	{
		fprintf(stderr, "SDL_Init: %s\n", SDL_GetError());
		return 1;
	}
	char font[700];
	snprintf(font, sizeof(font), "%s/res/font.ttf", A.base);
	FILE *ff = fopen(font, "rb");
	if(ff) fclose(ff);
	else snprintf(font, sizeof(font), "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf");
	if(!tp_ui_init(&A.ui, font))
		return 1;
	tp_input_init(&A.cfg);

	A.disc_mutex = SDL_CreateMutex();
	if(!disc_start())
		message("Network", "Couldn't start console discovery.", "Is Wi-Fi on?", NULL, SCR_HOSTS);
	A.screen = A.screen == SCR_MESSAGE ? SCR_MESSAGE : SCR_HOSTS;

	while(!A.in.quit)
	{
		tp_input_poll(&A.in, &A.cfg);
		hosts_rebuild();

		switch(A.screen)
		{
			case SCR_HOSTS: update_hosts(); break;
			case SCR_PIN: update_pin(); break;
			case SCR_REGISTERING: update_registering(); break;
			case SCR_WAKING: update_waking(); break;
			case SCR_STREAM: update_stream(); break;
			case SCR_INPUT_TEST: update_input_test(); break;
			case SCR_CONFIRM_FORGET:
				if(A.in.pressed[TB_EAST] && A.target.regist_idx >= 0)
				{
					int i = A.target.regist_idx;
					memmove(&A.reg.items[i], &A.reg.items[i + 1], (size_t)(A.reg.count - i - 1) * sizeof(A.reg.items[0]));
					A.reg.count--;
					tp_registry_save(&A.reg, A.hosts_path);
					A.screen = SCR_HOSTS;
				}
				else if(A.in.pressed[TB_SOUTH])
					A.screen = SCR_HOSTS;
				break;
			case SCR_MESSAGE:
				if(A.in.pressed[TB_EAST] || A.in.pressed[TB_SOUTH])
					A.screen = A.msg_return;
				break;
		}

		if(A.screen == SCR_STREAM)
		{
			tp_stream_render(&A.ui, A.show_stats);
			uint32_t t = SDL_GetTicks() - A.stream_started;
			if(tp_stream_state() == TS_CONNECTING)
				draw_center_status("Connecting", A.target.name);
			else if(t < 6000)
			{
				tp_ui_rect(&A.ui, 0, A.ui.h - 50, A.ui.w, 50, (SDL_Color){0, 0, 0, 160});
				tp_ui_text_center(&A.ui, A.ui.font_small, A.ui.h - 40, TP_WHITE,
					"MENU = PS    MENU+SELECT = touchpad    MENU+L1 = stats    hold MENU+START = disconnect");
			}
		}
		else if(A.screen == SCR_PIN && A.pin_for == PIN_FOR_LOGIN)
			draw_pin();
		else
		{
			tp_ui_clear(&A.ui);
			switch(A.screen)
			{
				case SCR_HOSTS: draw_hosts(); break;
				case SCR_PIN: draw_pin(); break;
				case SCR_REGISTERING: draw_center_status("Pairing", "Talking to the console - B to cancel"); break;
				case SCR_WAKING: draw_center_status("Waking console", "Waiting for it to come out of rest mode - B to cancel"); break;
				case SCR_MESSAGE: draw_message(); break;
				case SCR_INPUT_TEST: draw_input_test(); break;
				case SCR_CONFIRM_FORGET:
					tp_ui_text_center(&A.ui, A.ui.font_big, 220, TP_WHITE, "Forget %s?", A.target.name);
					tp_ui_text_center(&A.ui, A.ui.font_small, 320, TP_DIM, "You'll need to pair again with a new PIN.");
					footer("A forget    B keep");
					break;
				default: break;
			}
			// Menus don't need full CPU; vsync usually paces us, this covers when it doesn't.
			SDL_Delay(8);
		}
		SDL_RenderPresent(A.ui.renderer);
	}

	if(A.screen == SCR_STREAM || A.screen == SCR_PIN)
		tp_stream_stop();
	if(A.regist_running)
		regist_finish();
	if(A.disc_running)
		chiaki_discovery_service_fini(&A.disc);
	tp_input_fini();
	tp_ui_fini(&A.ui);
	SDL_Quit();
	return 0;
}
