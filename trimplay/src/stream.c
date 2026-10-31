// One Remote Play session: libchiaki networking + our own H.264 decode
// (tuned for the Smart Pro's four Cortex-A53 cores) + SDL audio/video.
#include "app.h"

#include <stdatomic.h>
#include <stdio.h>
#include <string.h>

#include <chiaki/opusdecoder.h>

#include <libavcodec/avcodec.h>
#include <libavutil/frame.h>

static struct {
	ChiakiSession session;
	ChiakiOpusDecoder opus;
	bool session_inited, opus_inited;
	char host[64];

	AVCodecContext *codec;
	AVPacket *pkt;
	AVFrame *decoded;          // decoder scratch (video thread only)
	AVFrame *pending;          // newest decoded frame, handed to the render thread
	bool pending_new;
	SDL_mutex *frame_mutex;
	SDL_Texture *tex;
	int tex_w, tex_h;
	AVFrame *shown;            // frame being displayed (render thread only)

	SDL_AudioDeviceID audio;
	uint32_t audio_rate, audio_channels;
	uint32_t audio_cap_bytes;

	atomic_int state;          // TpStreamState
	atomic_int rumble;         // (left << 8 | right) + 1, 0 = none pending
	ChiakiQuitReason quit_reason;
	char quit_text[160];

	// stats
	atomic_uint frames_decoded, frames_lost, decode_us_total;
	uint32_t stats_at, fps_shown, lost_shown, decode_ms_shown, last_frames;
} S;

// ---------------------------------------------------------------- video

static bool video_init(const TpConfig *cfg)
{
	const AVCodec *c = avcodec_find_decoder(AV_CODEC_ID_H264);
	if(!c)
		return false;
	S.codec = avcodec_alloc_context3(c);
	if(!S.codec)
		return false;
	S.codec->thread_count = cfg->decode_threads;
	S.codec->thread_type = cfg->decode_frame_threads ? FF_THREAD_FRAME : FF_THREAD_SLICE;
	S.codec->flags |= AV_CODEC_FLAG_LOW_DELAY;
	S.codec->flags2 |= AV_CODEC_FLAG2_FAST;
	if(cfg->fast_decode)
		S.codec->skip_loop_filter = AVDISCARD_ALL;
	if(avcodec_open2(S.codec, c, NULL) < 0)
		return false;
	S.pkt = av_packet_alloc();
	S.decoded = av_frame_alloc();
	S.pending = av_frame_alloc();
	S.shown = av_frame_alloc();
	S.frame_mutex = SDL_CreateMutex();
	return S.pkt && S.decoded && S.pending && S.shown && S.frame_mutex;
}

static void video_fini(void)
{
	if(S.codec) avcodec_free_context(&S.codec);
	if(S.pkt) av_packet_free(&S.pkt);
	if(S.decoded) av_frame_free(&S.decoded);
	if(S.pending) av_frame_free(&S.pending);
	if(S.shown) av_frame_free(&S.shown);
	if(S.frame_mutex) SDL_DestroyMutex(S.frame_mutex);
	S.frame_mutex = NULL;
	if(S.tex) SDL_DestroyTexture(S.tex);
	S.tex = NULL;
}

// Called on libchiaki's video thread with one Annex-B H.264 access unit.
static bool video_cb(uint8_t *buf, size_t buf_size, int32_t frames_lost, bool frame_recovered, void *user)
{
	(void)frame_recovered;
	(void)user;
	if(frames_lost > 0)
		atomic_fetch_add(&S.frames_lost, (unsigned)frames_lost);

	uint64_t t0 = SDL_GetPerformanceCounter();
	S.pkt->data = buf;
	S.pkt->size = (int)buf_size;
	int r = avcodec_send_packet(S.codec, S.pkt);
	if(r < 0 && r != AVERROR(EAGAIN))
		return false;
	bool got = false;
	while(avcodec_receive_frame(S.codec, S.decoded) == 0)
	{
		got = true;
		SDL_LockMutex(S.frame_mutex);
		av_frame_unref(S.pending);
		av_frame_move_ref(S.pending, S.decoded);
		S.pending_new = true;
		SDL_UnlockMutex(S.frame_mutex);
		atomic_fetch_add(&S.frames_decoded, 1);
	}
	if(got)
	{
		uint64_t us = (SDL_GetPerformanceCounter() - t0) * 1000000 / SDL_GetPerformanceFrequency();
		atomic_fetch_add(&S.decode_us_total, (unsigned)us);
		if(atomic_load(&S.state) == TS_CONNECTING)
			atomic_store(&S.state, TS_STREAMING);
	}
	return true;
}

void tp_stream_render(TpUi *ui, bool show_stats)
{
	if(S.frame_mutex)
	{
		SDL_LockMutex(S.frame_mutex);
		if(S.pending_new)
		{
			av_frame_unref(S.shown);
			av_frame_move_ref(S.shown, S.pending);
			S.pending_new = false;
		}
		SDL_UnlockMutex(S.frame_mutex);
	}

	AVFrame *f = S.shown;
	if(f && f->data[0] && (f->format == AV_PIX_FMT_YUV420P || f->format == AV_PIX_FMT_YUVJ420P))
	{
		if(!S.tex || S.tex_w != f->width || S.tex_h != f->height)
		{
			if(S.tex) SDL_DestroyTexture(S.tex);
			S.tex = SDL_CreateTexture(ui->renderer, SDL_PIXELFORMAT_IYUV, SDL_TEXTUREACCESS_STREAMING, f->width, f->height);
			S.tex_w = f->width;
			S.tex_h = f->height;
		}
		if(S.tex)
			SDL_UpdateYUVTexture(S.tex, NULL, f->data[0], f->linesize[0], f->data[1], f->linesize[1], f->data[2], f->linesize[2]);
	}

	SDL_SetRenderDrawColor(ui->renderer, 0, 0, 0, 255);
	SDL_RenderClear(ui->renderer);
	if(S.tex)
	{
		// Letterbox to 16:9 (the Smart Pro panel already is, so this is a full-screen blit).
		int w = ui->w, h = ui->w * S.tex_h / S.tex_w;
		if(h > ui->h) { h = ui->h; w = ui->h * S.tex_w / S.tex_h; }
		SDL_Rect dst = {(ui->w - w) / 2, (ui->h - h) / 2, w, h};
		SDL_RenderCopy(ui->renderer, S.tex, NULL, &dst);
	}

	uint32_t now = SDL_GetTicks();
	if(now - S.stats_at >= 1000)
	{
		unsigned frames = atomic_load(&S.frames_decoded);
		unsigned delta = frames - S.last_frames;
		S.fps_shown = delta * 1000 / (now - S.stats_at ? now - S.stats_at : 1);
		S.decode_ms_shown = delta ? atomic_exchange(&S.decode_us_total, 0) / delta / 1000 : 0;
		S.lost_shown = atomic_exchange(&S.frames_lost, 0);
		S.last_frames = frames;
		S.stats_at = now;
	}
	if(show_stats)
	{
		tp_ui_rect(ui, 8, 8, 600, 34, (SDL_Color){0, 0, 0, 150});
		tp_ui_text(ui, ui->font_small, 16, 12, TP_WHITE, "%dx%d  %u fps  decode %u ms  lost %u  audio %u ms",
			S.tex_w, S.tex_h, S.fps_shown, S.decode_ms_shown, S.lost_shown,
			S.audio && S.audio_rate ? (unsigned)(SDL_GetQueuedAudioSize(S.audio) * 1000ull /
				(S.audio_rate * S.audio_channels * 2)) : 0);
	}
}

// ---------------------------------------------------------------- audio

static void audio_settings_cb(uint32_t channels, uint32_t rate, void *user)
{
	const TpConfig *cfg = user;
	if(S.audio)
		SDL_CloseAudioDevice(S.audio);
	SDL_AudioSpec want = {0}, have;
	want.freq = (int)rate;
	want.format = AUDIO_S16SYS;
	want.channels = (Uint8)channels;
	want.samples = 512;
	S.audio = SDL_OpenAudioDevice(NULL, 0, &want, &have, 0);
	if(!S.audio)
	{
		SDL_Log("Audio open failed: %s", SDL_GetError());
		return;
	}
	S.audio_rate = rate;
	S.audio_channels = channels;
	S.audio_cap_bytes = rate * channels * 2 * (uint32_t)cfg->audio_latency_ms / 1000;
	SDL_PauseAudioDevice(S.audio, 0);
}

static void audio_frame_cb(int16_t *buf, size_t samples_count, void *user)
{
	(void)user;
	if(!S.audio)
		return;
	// Wi-Fi jitter piles audio up; dropping the backlog keeps lip-sync tight.
	if(SDL_GetQueuedAudioSize(S.audio) > S.audio_cap_bytes)
		SDL_ClearQueuedAudio(S.audio);
	SDL_QueueAudio(S.audio, buf, (Uint32)(samples_count * S.audio_channels * sizeof(int16_t)));
}

// ---------------------------------------------------------------- session

static void event_cb(ChiakiEvent *event, void *user)
{
	(void)user;
	switch(event->type)
	{
		case CHIAKI_EVENT_CONNECTED:
			SDL_Log("Connected");
			break;
		case CHIAKI_EVENT_LOGIN_PIN_REQUEST:
			atomic_store(&S.state, TS_LOGIN_PIN);
			break;
		case CHIAKI_EVENT_RUMBLE:
			atomic_store(&S.rumble, (event->rumble.left << 8 | event->rumble.right) + 1);
			break;
		case CHIAKI_EVENT_QUIT:
			S.quit_reason = event->quit.reason;
			snprintf(S.quit_text, sizeof(S.quit_text), "%s%s%s", chiaki_quit_reason_string(event->quit.reason),
				event->quit.reason_str ? ": " : "", event->quit.reason_str ? event->quit.reason_str : "");
			atomic_store(&S.state, TS_ENDED);
			break;
		default:
			break;
	}
}

bool tp_stream_start(const TpConfig *cfg, ChiakiLog *log, const TpRegistered *reg,
		const char *addr, bool ps5, TpUi *ui)
{
	(void)ui;
	memset(&S, 0, sizeof(S));
	atomic_store(&S.state, TS_CONNECTING);
	snprintf(S.host, sizeof(S.host), "%s", addr);

	if(!video_init(cfg))
	{
		snprintf(S.quit_text, sizeof(S.quit_text), "Could not start the H.264 decoder");
		video_fini();
		atomic_store(&S.state, TS_ENDED);
		return false;
	}

	ChiakiConnectInfo ci;
	memset(&ci, 0, sizeof(ci));
	ci.ps5 = ps5;
	ci.host = S.host;
	memcpy(ci.regist_key, reg->regist_key, sizeof(ci.regist_key));
	memcpy(ci.morning, reg->rp_key, sizeof(ci.morning));
	ChiakiVideoResolutionPreset res = cfg->resolution == 360 ? CHIAKI_VIDEO_RESOLUTION_PRESET_360p
		: cfg->resolution == 540 ? CHIAKI_VIDEO_RESOLUTION_PRESET_540p
		: cfg->resolution == 1080 ? CHIAKI_VIDEO_RESOLUTION_PRESET_1080p
		: CHIAKI_VIDEO_RESOLUTION_PRESET_720p;
	chiaki_connect_video_profile_preset(&ci.video_profile, res,
		cfg->fps == 60 ? CHIAKI_VIDEO_FPS_PRESET_60 : CHIAKI_VIDEO_FPS_PRESET_30);
	if(cfg->bitrate > 0)
		ci.video_profile.bitrate = (unsigned)cfg->bitrate;
	// This decoder is H.264 only; PS5s will also stream H.264 when asked.
	ci.video_profile.codec = CHIAKI_CODEC_H264;
	ci.video_profile_auto_downgrade = true;
	ci.enable_keyboard = false;
	ci.enable_dualsense = false;
	ci.packet_loss_max = 0.05;
	ci.enable_idr_on_fec_failure = true;

	ChiakiErrorCode err = chiaki_session_init(&S.session, &ci, log);
	if(err != CHIAKI_ERR_SUCCESS)
	{
		snprintf(S.quit_text, sizeof(S.quit_text), "Session init failed: %s", chiaki_error_string(err));
		video_fini();
		atomic_store(&S.state, TS_ENDED);
		return false;
	}
	S.session_inited = true;

	chiaki_opus_decoder_init(&S.opus, log);
	S.opus_inited = true;
	chiaki_opus_decoder_set_cb(&S.opus, audio_settings_cb, audio_frame_cb, (void *)cfg);
	ChiakiAudioSink sink;
	chiaki_opus_decoder_get_sink(&S.opus, &sink);
	chiaki_session_set_audio_sink(&S.session, &sink);
	chiaki_session_set_video_sample_cb(&S.session, video_cb, NULL);
	chiaki_session_set_event_cb(&S.session, event_cb, NULL);

	err = chiaki_session_start(&S.session);
	if(err != CHIAKI_ERR_SUCCESS)
	{
		snprintf(S.quit_text, sizeof(S.quit_text), "Session start failed: %s", chiaki_error_string(err));
		chiaki_session_fini(&S.session);
		S.session_inited = false;
		chiaki_opus_decoder_fini(&S.opus);
		S.opus_inited = false;
		video_fini();
		atomic_store(&S.state, TS_ENDED);
		return false;
	}
	S.stats_at = SDL_GetTicks();
	return true;
}

TpStreamState tp_stream_state(void)
{
	int r = atomic_exchange(&S.rumble, 0);
	if(r)
		tp_input_rumble((uint8_t)((r - 1) >> 8), (uint8_t)((r - 1) & 0xff));
	return (TpStreamState)atomic_load(&S.state);
}

void tp_stream_set_controller(ChiakiControllerState *st)
{
	if(S.session_inited)
		chiaki_session_set_controller_state(&S.session, st);
}

void tp_stream_login_pin(const char *pin)
{
	if(!S.session_inited)
		return;
	atomic_store(&S.state, TS_CONNECTING);
	chiaki_session_set_login_pin(&S.session, (const uint8_t *)pin, strlen(pin));
}

const char *tp_stream_end_reason(void)
{
	return S.quit_text[0] ? S.quit_text : "Disconnected";
}

void tp_stream_stop(void)
{
	if(S.session_inited)
	{
		chiaki_session_stop(&S.session);
		chiaki_session_join(&S.session);
		chiaki_session_fini(&S.session);
		S.session_inited = false;
	}
	if(S.opus_inited)
	{
		chiaki_opus_decoder_fini(&S.opus);
		S.opus_inited = false;
	}
	if(S.audio)
	{
		SDL_CloseAudioDevice(S.audio);
		S.audio = 0;
	}
	video_fini();
	atomic_store(&S.state, TS_IDLE);
}
