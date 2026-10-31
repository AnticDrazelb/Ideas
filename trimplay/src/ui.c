// Minimal immediate-mode text drawing with a small texture cache.
#include "app.h"

#include <stdarg.h>
#include <stdio.h>
#include <string.h>

#define CACHE_N 96

typedef struct {
	TTF_Font *font;
	SDL_Color color;
	char text[160];
	SDL_Texture *tex;
	int w, h;
	uint32_t used;
} CacheEntry;

static CacheEntry cache[CACHE_N];
static uint32_t cache_clock;

bool tp_ui_init(TpUi *ui, const char *font_path)
{
	memset(ui, 0, sizeof(*ui));
	if(TTF_Init() != 0)
	{
		SDL_Log("TTF_Init: %s", TTF_GetError());
		return false;
	}
	// The Smart Pro panel is 1280x720; on a desktop this opens a window that size.
	ui->window = SDL_CreateWindow("TrimPlay", SDL_WINDOWPOS_CENTERED, SDL_WINDOWPOS_CENTERED,
		1280, 720, SDL_getenv("TRIMPLAY_WINDOWED") ? 0 : SDL_WINDOW_FULLSCREEN);
	if(!ui->window)
	{
		SDL_Log("SDL_CreateWindow: %s", SDL_GetError());
		return false;
	}
	ui->renderer = SDL_CreateRenderer(ui->window, -1, SDL_RENDERER_ACCELERATED | SDL_RENDERER_PRESENTVSYNC);
	if(!ui->renderer)
		ui->renderer = SDL_CreateRenderer(ui->window, -1, 0);
	if(!ui->renderer)
	{
		SDL_Log("SDL_CreateRenderer: %s", SDL_GetError());
		return false;
	}
	SDL_RendererInfo info;
	if(SDL_GetRendererInfo(ui->renderer, &info) == 0)
		SDL_Log("Renderer: %s", info.name);
	SDL_GetRendererOutputSize(ui->renderer, &ui->w, &ui->h);
	SDL_SetHint(SDL_HINT_RENDER_SCALE_QUALITY, "linear");

	int base = ui->h / 24; // 30px on 720p
	ui->font = TTF_OpenFont(font_path, base);
	ui->font_big = TTF_OpenFont(font_path, base * 2);
	ui->font_small = TTF_OpenFont(font_path, base * 2 / 3);
	if(!ui->font || !ui->font_big || !ui->font_small)
	{
		SDL_Log("Font %s: %s", font_path, TTF_GetError());
		return false;
	}
	SDL_ShowCursor(SDL_DISABLE);
	return true;
}

void tp_ui_fini(TpUi *ui)
{
	for(int i = 0; i < CACHE_N; i++)
		if(cache[i].tex)
			SDL_DestroyTexture(cache[i].tex);
	memset(cache, 0, sizeof(cache));
	if(ui->font) TTF_CloseFont(ui->font);
	if(ui->font_big) TTF_CloseFont(ui->font_big);
	if(ui->font_small) TTF_CloseFont(ui->font_small);
	if(ui->renderer) SDL_DestroyRenderer(ui->renderer);
	if(ui->window) SDL_DestroyWindow(ui->window);
	TTF_Quit();
}

void tp_ui_clear(TpUi *ui)
{
	SDL_SetRenderDrawColor(ui->renderer, TP_BG.r, TP_BG.g, TP_BG.b, 255);
	SDL_RenderClear(ui->renderer);
}

void tp_ui_rect(TpUi *ui, int x, int y, int w, int h, SDL_Color c)
{
	SDL_SetRenderDrawBlendMode(ui->renderer, SDL_BLENDMODE_BLEND);
	SDL_SetRenderDrawColor(ui->renderer, c.r, c.g, c.b, c.a);
	SDL_Rect r = {x, y, w, h};
	SDL_RenderFillRect(ui->renderer, &r);
}

static CacheEntry *lookup(TpUi *ui, TTF_Font *f, SDL_Color c, const char *s)
{
	CacheEntry *victim = &cache[0];
	for(int i = 0; i < CACHE_N; i++)
	{
		CacheEntry *e = &cache[i];
		if(e->tex && e->font == f && !memcmp(&e->color, &c, sizeof(c)) && !strcmp(e->text, s))
		{
			e->used = ++cache_clock;
			return e;
		}
		if(!e->tex || e->used < victim->used)
			victim = e;
	}
	SDL_Surface *surf = TTF_RenderUTF8_Blended(f, s, c);
	if(!surf)
		return NULL;
	if(victim->tex)
		SDL_DestroyTexture(victim->tex);
	victim->tex = SDL_CreateTextureFromSurface(ui->renderer, surf);
	victim->w = surf->w;
	victim->h = surf->h;
	SDL_FreeSurface(surf);
	victim->font = f;
	victim->color = c;
	snprintf(victim->text, sizeof(victim->text), "%s", s);
	victim->used = ++cache_clock;
	return victim->tex ? victim : NULL;
}

static void draw(TpUi *ui, TTF_Font *f, int x, int y, bool center, SDL_Color c, const char *fmt, va_list ap)
{
	char buf[160];
	vsnprintf(buf, sizeof(buf), fmt, ap);
	if(!buf[0])
		return;
	CacheEntry *e = lookup(ui, f, c, buf);
	if(!e)
		return;
	SDL_Rect r = {center ? (ui->w - e->w) / 2 : x, y, e->w, e->h};
	SDL_RenderCopy(ui->renderer, e->tex, NULL, &r);
}

void tp_ui_text(TpUi *ui, TTF_Font *f, int x, int y, SDL_Color c, const char *fmt, ...)
{
	va_list ap;
	va_start(ap, fmt);
	draw(ui, f, x, y, false, c, fmt, ap);
	va_end(ap);
}

void tp_ui_text_center(TpUi *ui, TTF_Font *f, int y, SDL_Color c, const char *fmt, ...)
{
	va_list ap;
	va_start(ap, fmt);
	draw(ui, f, 0, y, true, c, fmt, ap);
	va_end(ap);
}
