// Input: turns whatever the TrimUI's SDL exposes (game controller, raw
// joystick, or keyboard events - firmwares differ) into logical buttons.
#include "app.h"

#include <stdio.h>
#include <string.h>

static SDL_GameController *pad;
static SDL_Joystick *joy;

// Desktop-friendly keyboard defaults (scancodes). Overridable in [keys].
static const int default_keys[TB_COUNT] = {
	[TB_SOUTH] = SDL_SCANCODE_X, [TB_EAST] = SDL_SCANCODE_C,
	[TB_WEST] = SDL_SCANCODE_Z, [TB_NORTH] = SDL_SCANCODE_S,
	[TB_UP] = SDL_SCANCODE_UP, [TB_DOWN] = SDL_SCANCODE_DOWN,
	[TB_LEFT] = SDL_SCANCODE_LEFT, [TB_RIGHT] = SDL_SCANCODE_RIGHT,
	[TB_L1] = SDL_SCANCODE_Q, [TB_R1] = SDL_SCANCODE_W,
	[TB_L2] = SDL_SCANCODE_1, [TB_R2] = SDL_SCANCODE_2,
	[TB_L3] = SDL_SCANCODE_3, [TB_R3] = SDL_SCANCODE_4,
	[TB_SELECT] = SDL_SCANCODE_BACKSPACE, [TB_START] = SDL_SCANCODE_RETURN,
	[TB_MENU] = SDL_SCANCODE_ESCAPE,
};

static const SDL_GameControllerButton pad_map[TB_COUNT] = {
	[TB_SOUTH] = SDL_CONTROLLER_BUTTON_A, [TB_EAST] = SDL_CONTROLLER_BUTTON_B,
	[TB_WEST] = SDL_CONTROLLER_BUTTON_X, [TB_NORTH] = SDL_CONTROLLER_BUTTON_Y,
	[TB_UP] = SDL_CONTROLLER_BUTTON_DPAD_UP, [TB_DOWN] = SDL_CONTROLLER_BUTTON_DPAD_DOWN,
	[TB_LEFT] = SDL_CONTROLLER_BUTTON_DPAD_LEFT, [TB_RIGHT] = SDL_CONTROLLER_BUTTON_DPAD_RIGHT,
	[TB_L1] = SDL_CONTROLLER_BUTTON_LEFTSHOULDER, [TB_R1] = SDL_CONTROLLER_BUTTON_RIGHTSHOULDER,
	[TB_L2] = SDL_CONTROLLER_BUTTON_INVALID, [TB_R2] = SDL_CONTROLLER_BUTTON_INVALID,
	[TB_L3] = SDL_CONTROLLER_BUTTON_LEFTSTICK, [TB_R3] = SDL_CONTROLLER_BUTTON_RIGHTSTICK,
	[TB_SELECT] = SDL_CONTROLLER_BUTTON_BACK, [TB_START] = SDL_CONTROLLER_BUTTON_START,
	[TB_MENU] = SDL_CONTROLLER_BUTTON_GUIDE,
};

static void open_first(void)
{
	for(int i = 0; i < SDL_NumJoysticks() && !pad && !joy; i++)
	{
		if(SDL_IsGameController(i))
		{
			pad = SDL_GameControllerOpen(i);
			if(pad)
				SDL_Log("Game controller: %s", SDL_GameControllerName(pad));
		}
		else
		{
			joy = SDL_JoystickOpen(i);
			if(joy)
				SDL_Log("Raw joystick: %s (%d buttons, %d axes, %d hats)", SDL_JoystickName(joy),
					SDL_JoystickNumButtons(joy), SDL_JoystickNumAxes(joy), SDL_JoystickNumHats(joy));
		}
	}
}

void tp_input_init(const TpConfig *cfg)
{
	(void)cfg;
	SDL_GameControllerEventState(SDL_ENABLE);
	SDL_JoystickEventState(SDL_ENABLE);
	open_first();
}

void tp_input_fini(void)
{
	if(pad) SDL_GameControllerClose(pad);
	if(joy) SDL_JoystickClose(joy);
	pad = NULL;
	joy = NULL;
}

void tp_input_rumble(uint8_t left, uint8_t right)
{
	uint16_t lo = (uint16_t)(left * 257), hi = (uint16_t)(right * 257);
	if(pad)
		SDL_GameControllerRumble(pad, lo, hi, 500);
	else if(joy)
		SDL_JoystickRumble(joy, lo, hi, 500);
}

static int16_t deadzone(int v, int dz)
{
	if(v > -dz && v < dz)
		return 0;
	if(v > 32767) v = 32767;
	if(v < -32768) v = -32768;
	return (int16_t)v;
}

static void note_raw(TpInput *in, const char *fmt, int a, int b)
{
	snprintf(in->last_raw, sizeof(in->last_raw), fmt, a, b);
	in->last_raw_ms = SDL_GetTicks();
}

void tp_input_poll(TpInput *in, const TpConfig *cfg)
{
	bool prev[TB_COUNT];
	memcpy(prev, in->down, sizeof(prev));

	SDL_Event e;
	while(SDL_PollEvent(&e))
	{
		switch(e.type)
		{
			case SDL_QUIT:
				in->quit = true;
				break;
			case SDL_CONTROLLERDEVICEADDED:
			case SDL_JOYDEVICEADDED:
				if(!pad && !joy)
					open_first();
				break;
			case SDL_KEYDOWN:
				if(!e.key.repeat)
				{
					snprintf(in->last_raw, sizeof(in->last_raw), "key scancode %d  (%s)",
						(int)e.key.keysym.scancode, SDL_GetScancodeName(e.key.keysym.scancode));
					in->last_raw_ms = SDL_GetTicks();
				}
				break;
			case SDL_JOYBUTTONDOWN:
				note_raw(in, "joystick button %d", e.jbutton.button, 0);
				break;
			case SDL_JOYHATMOTION:
				if(e.jhat.value)
					note_raw(in, "joystick hat %d value %d", e.jhat.hat, e.jhat.value);
				break;
			case SDL_JOYAXISMOTION:
				if(e.jaxis.value > 16000 || e.jaxis.value < -16000)
					note_raw(in, "joystick axis %d value %d", e.jaxis.axis, e.jaxis.value);
				break;
			default:
				break;
		}
	}

	memset(in->down, 0, sizeof(in->down));
	in->lx = in->ly = in->rx = in->ry = 0;

	// Keyboard (some TrimUI SDL builds deliver the face buttons as keys).
	const Uint8 *ks = SDL_GetKeyboardState(NULL);
	for(int b = 0; b < TB_COUNT; b++)
	{
		int sc = cfg->map_key[b] >= 0 ? cfg->map_key[b] : default_keys[b];
		if(sc > 0 && sc < SDL_NUM_SCANCODES && ks[sc])
			in->down[b] = true;
	}

	int dz = cfg->stick_deadzone;
	if(pad)
	{
		for(int b = 0; b < TB_COUNT; b++)
			if(pad_map[b] != SDL_CONTROLLER_BUTTON_INVALID && SDL_GameControllerGetButton(pad, pad_map[b]))
				in->down[b] = true;
		if(SDL_GameControllerGetAxis(pad, SDL_CONTROLLER_AXIS_TRIGGERLEFT) > 8000) in->down[TB_L2] = true;
		if(SDL_GameControllerGetAxis(pad, SDL_CONTROLLER_AXIS_TRIGGERRIGHT) > 8000) in->down[TB_R2] = true;
		in->lx = deadzone(SDL_GameControllerGetAxis(pad, SDL_CONTROLLER_AXIS_LEFTX), dz);
		in->ly = deadzone(SDL_GameControllerGetAxis(pad, SDL_CONTROLLER_AXIS_LEFTY), dz);
		in->rx = deadzone(SDL_GameControllerGetAxis(pad, SDL_CONTROLLER_AXIS_RIGHTX), dz);
		in->ry = deadzone(SDL_GameControllerGetAxis(pad, SDL_CONTROLLER_AXIS_RIGHTY), dz);
	}
	else if(joy)
	{
		int nb = SDL_JoystickNumButtons(joy);
		for(int b = 0; b < TB_COUNT; b++)
		{
			int jb = cfg->map_joy[b];
			if(jb >= 0 && jb < nb && SDL_JoystickGetButton(joy, jb))
				in->down[b] = true;
		}
		if(SDL_JoystickNumHats(joy) > 0)
		{
			Uint8 h = SDL_JoystickGetHat(joy, 0);
			if(h & SDL_HAT_UP) in->down[TB_UP] = true;
			if(h & SDL_HAT_DOWN) in->down[TB_DOWN] = true;
			if(h & SDL_HAT_LEFT) in->down[TB_LEFT] = true;
			if(h & SDL_HAT_RIGHT) in->down[TB_RIGHT] = true;
		}
		int na = SDL_JoystickNumAxes(joy);
		if(na >= 2)
		{
			in->lx = deadzone(SDL_JoystickGetAxis(joy, 0), dz);
			in->ly = deadzone(SDL_JoystickGetAxis(joy, 1), dz);
		}
		if(na >= 4)
		{
			in->rx = deadzone(SDL_JoystickGetAxis(joy, 2), dz);
			in->ry = deadzone(SDL_JoystickGetAxis(joy, 3), dz);
		}
	}

	for(int b = 0; b < TB_COUNT; b++)
		in->pressed[b] = in->down[b] && !prev[b];
}
