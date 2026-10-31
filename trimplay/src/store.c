// Config (trimplay.ini) and paired-console storage (hosts.ini).
#include "app.h"

#include <ctype.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <strings.h>

const char *tp_button_names[TB_COUNT] = {
	"south", "east", "west", "north",
	"up", "down", "left", "right",
	"l1", "r1", "l2", "r2", "l3", "r3",
	"select", "start", "menu",
};

static char *trim(char *s)
{
	while(isspace((unsigned char)*s)) s++;
	char *e = s + strlen(s);
	while(e > s && isspace((unsigned char)e[-1])) *--e = '\0';
	return s;
}

static bool truthy(const char *v)
{
	return !strcmp(v, "1") || !strcasecmp(v, "true") || !strcasecmp(v, "yes") || !strcasecmp(v, "on");
}

static void copy_str(char *dst, size_t n, const char *src)
{
	snprintf(dst, n, "%s", src);
}

// Calls cb(section, key, value) for every key=value line.
typedef void (*IniCb)(const char *section, const char *key, const char *value, void *user);

static bool ini_parse(const char *path, IniCb cb, void *user)
{
	FILE *f = fopen(path, "r");
	if(!f)
		return false;
	char line[512], section[128] = "";
	while(fgets(line, sizeof(line), f))
	{
		char *s = trim(line);
		if(!*s || *s == '#' || *s == ';')
			continue;
		if(*s == '[')
		{
			char *e = strchr(s, ']');
			if(e) *e = '\0';
			copy_str(section, sizeof(section), trim(s + 1));
			continue;
		}
		char *eq = strchr(s, '=');
		if(!eq)
			continue;
		*eq = '\0';
		cb(section, trim(s), trim(eq + 1), user);
	}
	fclose(f);
	return true;
}

// ---------------------------------------------------------------- config

void tp_config_defaults(TpConfig *c)
{
	memset(c, 0, sizeof(*c));
	c->resolution = 720;
	c->fps = 30;
	c->bitrate = 0;
	c->decode_threads = 4;
	c->decode_frame_threads = false;
	c->fast_decode = false;
	c->audio_latency_ms = 120;
	c->stick_deadzone = 2500;
	c->swap_ab = false;
	c->show_stats = false;
	for(int i = 0; i < 32; i++)
	{
		c->map_key[i] = -1;
		c->map_joy[i] = -1;
	}
}

static int button_by_name(const char *name)
{
	for(int i = 0; i < TB_COUNT; i++)
		if(!strcasecmp(name, tp_button_names[i]))
			return i;
	return -1;
}

static void config_cb(const char *section, const char *k, const char *v, void *user)
{
	TpConfig *c = user;
	if(!strcasecmp(section, "keys") || !strcasecmp(section, "joystick"))
	{
		int b = button_by_name(k);
		if(b < 0 || !*v)
			return;
		if(!strcasecmp(section, "keys"))
			c->map_key[b] = atoi(v);
		else
			c->map_joy[b] = atoi(v);
		return;
	}
	if(!strcasecmp(k, "psn_account_id")) copy_str(c->psn_account_id, sizeof(c->psn_account_id), v);
	else if(!strcasecmp(k, "host")) copy_str(c->manual_host, sizeof(c->manual_host), v);
	else if(!strcasecmp(k, "resolution")) c->resolution = atoi(v);
	else if(!strcasecmp(k, "fps")) c->fps = atoi(v);
	else if(!strcasecmp(k, "bitrate")) c->bitrate = atoi(v);
	else if(!strcasecmp(k, "decode_threads")) c->decode_threads = atoi(v);
	else if(!strcasecmp(k, "decode_frame_threads")) c->decode_frame_threads = truthy(v);
	else if(!strcasecmp(k, "fast_decode")) c->fast_decode = truthy(v);
	else if(!strcasecmp(k, "audio_latency_ms")) c->audio_latency_ms = atoi(v);
	else if(!strcasecmp(k, "stick_deadzone")) c->stick_deadzone = atoi(v);
	else if(!strcasecmp(k, "swap_ab")) c->swap_ab = truthy(v);
	else if(!strcasecmp(k, "show_stats")) c->show_stats = truthy(v);
	else if(!strcasecmp(k, "verbose_log")) c->verbose_log = truthy(v);
}

bool tp_config_load(TpConfig *c, const char *path)
{
	bool ok = ini_parse(path, config_cb, c);
	if(c->fps != 60) c->fps = 30;
	if(c->resolution != 360 && c->resolution != 540 && c->resolution != 1080) c->resolution = 720;
	if(c->audio_latency_ms < 40) c->audio_latency_ms = 40;
	return ok;
}

// ---------------------------------------------------------------- hex

void tp_hex_encode(const uint8_t *in, size_t n, char *out)
{
	static const char d[] = "0123456789abcdef";
	for(size_t i = 0; i < n; i++)
	{
		out[i * 2] = d[in[i] >> 4];
		out[i * 2 + 1] = d[in[i] & 0xf];
	}
	out[n * 2] = '\0';
}

static int hexval(char c)
{
	if(c >= '0' && c <= '9') return c - '0';
	c = (char)tolower((unsigned char)c);
	if(c >= 'a' && c <= 'f') return c - 'a' + 10;
	return -1;
}

bool tp_hex_decode(const char *in, uint8_t *out, size_t n)
{
	if(strlen(in) != n * 2)
		return false;
	for(size_t i = 0; i < n; i++)
	{
		int hi = hexval(in[i * 2]), lo = hexval(in[i * 2 + 1]);
		if(hi < 0 || lo < 0)
			return false;
		out[i] = (uint8_t)(hi << 4 | lo);
	}
	return true;
}

// ---------------------------------------------------------------- registry

int tp_registry_find(const TpRegistry *r, const char *mac)
{
	for(int i = 0; i < r->count; i++)
		if(!strcasecmp(r->items[i].mac, mac))
			return i;
	return -1;
}

int tp_registry_put(TpRegistry *r, const TpRegistered *h)
{
	int i = tp_registry_find(r, h->mac);
	if(i < 0)
	{
		if(r->count >= TP_MAX_HOSTS)
			return -1;
		i = r->count++;
	}
	r->items[i] = *h;
	return i;
}

static void registry_cb(const char *section, const char *k, const char *v, void *user)
{
	TpRegistry *r = user;
	if(strncasecmp(section, "host ", 5))
		return;
	const char *mac = section + 5;
	int i = tp_registry_find(r, mac);
	if(i < 0)
	{
		if(r->count >= TP_MAX_HOSTS)
			return;
		i = r->count++;
		memset(&r->items[i], 0, sizeof(r->items[i]));
		copy_str(r->items[i].mac, sizeof(r->items[i].mac), mac);
		for(char *p = r->items[i].mac; *p; p++) *p = (char)toupper((unsigned char)*p);
	}
	TpRegistered *h = &r->items[i];
	if(!strcasecmp(k, "nickname")) copy_str(h->nickname, sizeof(h->nickname), v);
	else if(!strcasecmp(k, "target")) h->target = (ChiakiTarget)atoi(v);
	else if(!strcasecmp(k, "last_ip")) copy_str(h->last_ip, sizeof(h->last_ip), v);
	else if(!strcasecmp(k, "rp_key")) tp_hex_decode(v, h->rp_key, sizeof(h->rp_key));
	else if(!strcasecmp(k, "regist_key"))
		tp_hex_decode(v, (uint8_t *)h->regist_key, sizeof(h->regist_key));
}

bool tp_registry_load(TpRegistry *r, const char *path)
{
	memset(r, 0, sizeof(*r));
	return ini_parse(path, registry_cb, r);
}

bool tp_registry_save(const TpRegistry *r, const char *path)
{
	char tmp[1024];
	snprintf(tmp, sizeof(tmp), "%s.tmp", path);
	FILE *f = fopen(tmp, "w");
	if(!f)
		return false;
	fprintf(f, "# Paired consoles. Written by TrimPlay - these keys let this device\n"
	           "# sign in to your console, so treat this file like a password.\n\n");
	for(int i = 0; i < r->count; i++)
	{
		const TpRegistered *h = &r->items[i];
		char rk[sizeof(h->regist_key) * 2 + 1], pk[sizeof(h->rp_key) * 2 + 1];
		tp_hex_encode((const uint8_t *)h->regist_key, sizeof(h->regist_key), rk);
		tp_hex_encode(h->rp_key, sizeof(h->rp_key), pk);
		fprintf(f, "[host %s]\nnickname=%s\ntarget=%d\nlast_ip=%s\nregist_key=%s\nrp_key=%s\n\n",
			h->mac, h->nickname, (int)h->target, h->last_ip, rk, pk);
	}
	bool ok = fflush(f) == 0;
	fclose(f);
	if(ok)
		ok = rename(tmp, path) == 0;
	return ok;
}
