// TrimPlay - PlayStation Remote Play front-end for the TrimUI Smart Pro.
// Built on libchiaki from chiaki-ng (AGPL-3.0). See ../README.md.
#ifndef TRIMPLAY_APP_H
#define TRIMPLAY_APP_H

#include <stdbool.h>
#include <stdint.h>
#include <stddef.h>

#include <SDL.h>
#include <SDL_ttf.h>

#include <chiaki/common.h>
#include <chiaki/log.h>
#include <chiaki/regist.h>
#include <chiaki/session.h>
#include <chiaki/controller.h>
#include <chiaki/discovery.h>

#define TP_MAX_HOSTS 16
#define TP_STR 128

// ---------------------------------------------------------------- config

typedef struct {
	char psn_account_id[64];   // base64, from PSN (see README)
	char manual_host[64];      // optional fixed console IP (skips broadcast)
	int resolution;            // 360 / 540 / 720 / 1080
	int fps;                   // 30 / 60
	int bitrate;               // kbps, 0 = preset default
	int decode_threads;        // libavcodec threads, 0 = auto
	bool decode_frame_threads; // frame threading: faster, adds latency
	bool fast_decode;          // skip deblocking: much faster, slight artifacts
	int audio_latency_ms;      // queued-audio cap before we drop
	int stick_deadzone;        // 0..32767
	bool swap_ab;              // swap Cross/Circle (positional by default)
	bool show_stats;
	bool verbose_log;
	int map_key[32];           // raw keyboard scancode per logical button, -1 = none
	int map_joy[32];           // raw joystick button per logical button, -1 = none
} TpConfig;

// Logical buttons, named by POSITION on the TrimUI face (south = bottom).
typedef enum {
	TB_SOUTH, TB_EAST, TB_WEST, TB_NORTH,
	TB_UP, TB_DOWN, TB_LEFT, TB_RIGHT,
	TB_L1, TB_R1, TB_L2, TB_R2, TB_L3, TB_R3,
	TB_SELECT, TB_START, TB_MENU,
	TB_COUNT
} TpButton;

extern const char *tp_button_names[TB_COUNT];

// ---------------------------------------------------------------- hosts

typedef struct {
	char mac[13];              // 12 hex chars, upper case
	char nickname[0x20];
	ChiakiTarget target;
	char regist_key[CHIAKI_SESSION_AUTH_SIZE];
	uint8_t rp_key[0x10];
	char last_ip[64];
} TpRegistered;

typedef struct {
	char mac[13];
	char name[TP_STR];
	char addr[64];
	char app[TP_STR];
	ChiakiDiscoveryHostState state;
	ChiakiTarget target;
	bool ps5;
	bool seen;                 // currently answering discovery
	int regist_idx;            // index into registered list, -1 if not paired
} TpHost;

typedef struct {
	TpRegistered items[TP_MAX_HOSTS];
	int count;
} TpRegistry;

// store.c
void tp_config_defaults(TpConfig *c);
bool tp_config_load(TpConfig *c, const char *path);
bool tp_registry_load(TpRegistry *r, const char *path);
bool tp_registry_save(const TpRegistry *r, const char *path);
int  tp_registry_find(const TpRegistry *r, const char *mac);
int  tp_registry_put(TpRegistry *r, const TpRegistered *h);
void tp_hex_encode(const uint8_t *in, size_t n, char *out);
bool tp_hex_decode(const char *in, uint8_t *out, size_t n);

// ---------------------------------------------------------------- input

typedef struct {
	bool down[TB_COUNT];
	bool pressed[TB_COUNT];    // edge this frame
	int16_t lx, ly, rx, ry;
	// Last raw event, for the input test screen.
	char last_raw[TP_STR];
	uint32_t last_raw_ms;
	bool quit;                 // window closed / SIGTERM
} TpInput;

void tp_input_init(const TpConfig *cfg);
void tp_input_poll(TpInput *in, const TpConfig *cfg);
void tp_input_rumble(uint8_t left, uint8_t right);
void tp_input_fini(void);

// ---------------------------------------------------------------- ui

typedef struct {
	SDL_Window *window;
	SDL_Renderer *renderer;
	TTF_Font *font, *font_big, *font_small;
	int w, h;
} TpUi;

bool tp_ui_init(TpUi *ui, const char *font_path);
void tp_ui_fini(TpUi *ui);
void tp_ui_clear(TpUi *ui);
void tp_ui_text(TpUi *ui, TTF_Font *f, int x, int y, SDL_Color c, const char *fmt, ...);
void tp_ui_text_center(TpUi *ui, TTF_Font *f, int y, SDL_Color c, const char *fmt, ...);
void tp_ui_rect(TpUi *ui, int x, int y, int w, int h, SDL_Color c);

#define TP_WHITE  ((SDL_Color){235, 238, 245, 255})
#define TP_DIM    ((SDL_Color){130, 140, 160, 255})
#define TP_ACCENT ((SDL_Color){ 70, 140, 255, 255})
#define TP_GOOD   ((SDL_Color){ 90, 210, 130, 255})
#define TP_WARN   ((SDL_Color){240, 180,  70, 255})
#define TP_BAD    ((SDL_Color){240,  90,  90, 255})
#define TP_BG     ((SDL_Color){ 14,  17,  26, 255})
#define TP_PANEL  ((SDL_Color){ 28,  33,  48, 255})

// ---------------------------------------------------------------- stream

typedef enum {
	TS_IDLE,
	TS_CONNECTING,
	TS_STREAMING,
	TS_LOGIN_PIN,              // console wants its 4-digit login passcode
	TS_ENDED,
} TpStreamState;

bool tp_stream_start(const TpConfig *cfg, ChiakiLog *log, const TpRegistered *reg,
		const char *addr, bool ps5, TpUi *ui);
TpStreamState tp_stream_state(void);
void tp_stream_set_controller(ChiakiControllerState *st);
void tp_stream_login_pin(const char *pin);
void tp_stream_render(TpUi *ui, bool show_stats);   // draws the latest frame
void tp_stream_stop(void);                          // blocks until torn down
const char *tp_stream_end_reason(void);

#endif
