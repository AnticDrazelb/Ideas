// THE PARTS OF Singularity/Cell THAT BOTH OF ITS PASSES NEED.
//
// A ShaderLab Pass is its own compilation unit: nothing declared in one is
// visible in the next, so the second pass cannot simply name the first's vertex
// shader. The surface pass and the x-ray pass run the SAME vertex stage — the
// arrival wave, the eversion, the exit burst, the depth key — and they have to,
// because a wire that did not travel with the cell it belongs to would come off
// it the moment anything moved.
//
// So it lives here once, and the two passes differ only in what they do with the
// fragment. That is also the honest description of what they are: one picture,
// asked two questions.
#ifndef SINGULARITY_CELL_INCLUDED
#define SINGULARITY_CELL_INCLUDED

#include "UnityCG.cginc"

struct appdata
{
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float2 uv     : TEXCOORD0;   // face-local 0..1, for the inset hairline
    float3 centre : TEXCOORD1;   // the CELL's centre in object space
    fixed4 color  : COLOR;       // r: reachable.  g: BFS distance.  b: distance from the transition's focus.
};

struct v2f
{
    float4 pos     : SV_POSITION;
    float2 uv      : TEXCOORD0;
    float  depth01 : TEXCOORD1;
    float3 vnrm    : TEXCOORD2;
    float  gone    : TEXCOORD3;   // how far through its own throw this cell is
    fixed4 reach   : COLOR;
};

fixed4 _ColFar, _ColNear;
float _HalfSpan, _Facing, _Edge, _EdgeLift, _Dim, _Reveal, _Unlit, _UnlitSat, _Gutter, _Round, _Peek;
float _Wave, _WaveDir, _WaveSoft;
float _Evert, _EvertSpread;
float4 _EvertAxis, _EvertSpin;
float _Burst, _BurstThrow, _BurstSpread, _BurstSpin;
fixed4 _WireCol;

// Wire.shader's, declared here because it shares this vertex stage
float _WireFar;

// Rodrigues. Both of the things that turn a cell about its own centre
// want it, and both of them have to turn the NORMAL by the same amount
// — see the note where the eversion uses it.
float3 Turn(float3 v, float3 axis, float ang)
{
    float cs = cos(ang), sn = sin(ang);
    return v * cs + cross(axis, v) * sn + axis * dot(axis, v) * (1.0 - cs);
}

v2f vert(appdata v)
{
    v2f o;

    // THE CUBE ARRIVES AND LEAVES ONE CELL AT A TIME.
    //
    // A board that appears all at once is a slide; a board that builds
    // outward from where the player will be standing is the machine
    // assembling itself, and it costs one lerp. Each cell shrinks to its
    // own centre until the front reaches it, which is why the vertex is
    // interpolated toward v.centre rather than scaled about the origin —
    // scaling about the origin would slide every cell across the board
    // instead of letting each one grow where it belongs.
    float waveD = v.color.b * 255.0;
    float w = _WaveDir > 0.0
        ? saturate((_Wave - waveD) / _WaveSoft)
        : saturate((waveD - _Wave) / _WaveSoft);
    float3 local = lerp(v.centre, v.vertex.xyz, w * w * (3.0 - 2.0 * w));

    // ---- EVERSION, ONE CELL AT A TIME ---------------------------
    //
    // The rule is that each column shows its FAR cell instead of its
    // near one, so the honest end state is every cell mirrored along
    // the camera axis. Doing that to the whole solid at once is one
    // line and reads as the object being squashed through a plane.
    //
    // Doing it PER CELL reads as what it is. Each cell travels to its
    // own mirror position and turns a half-circle about its own centre
    // on the way, and the cells do not leave together: the stagger is
    // keyed on depth, so the turn sweeps through the machine from the
    // face you are looking at to the one you are about to be looking
    // at. Every square on screen is a card flipping over, and what is
    // on the back of it is the cell that was hiding behind it — which
    // is not a metaphor for the rule, it is the rule.
    //
    // A half-circle about a face axis maps a cube onto itself, so the
    // settled board is exactly the mirrored board and nothing needs
    // to be undone at the end.
    float3 eCentre = v.centre;
    float3 nrm = v.normal;
    if (_Evert > 1e-5)
    {
        float3 ax = normalize(_EvertAxis.xyz);
        float along = dot(v.centre, ax);

        // where this cell sits along the view axis, 0 far .. 1 near —
        // and therefore when its turn starts
        // 2 * _HalfSpan is the full span of cell centres — the same
        // divisor the depth ramp below uses. Four would squeeze every
        // cell into the middle quarter of the sweep and the stagger
        // would be invisible.
        float t = saturate(along / (2.0 * _HalfSpan) + 0.5);

        // the whole sweep still finishes at _Evert = 1 however wide the
        // stagger is: the front is scaled up by the spread and each
        // cell subtracts its own share
        float k = saturate(_Evert * (1.0 + _EvertSpread) - t * _EvertSpread);
        float e = k * k * (3.0 - 2.0 * k);

        eCentre = v.centre - 2.0 * along * ax * e;

        // Rodrigues, about the cell's own centre. The spin axis is the
        // camera's right in object space, so every cell turns the same
        // way on screen no matter which way the engine is folded.
        //
        // THE NORMAL TURNS WITH IT. A half-circle maps ±Z and ±Y onto
        // their opposites, so a cell that has finished its flip is
        // showing the face whose AUTHORED normal points away from the
        // camera — and the fragment shader reads that normal to decide
        // whether a face is a back face, which it then discards. The
        // settled everted board survived that only because Cull is Off
        // and the far face of the same cell was standing in for it.
        // That is a coincidence, not a rendering.
        float3 sp = normalize(_EvertSpin.xyz);
        float3 rel = local - v.centre;
        float ang = 3.14159265 * e;
        rel = Turn(rel, sp, ang);
        nrm = Turn(nrm, sp, ang);

        local = eCentre + rel;
    }

    // ---- THE BOARD LEAVES IN PIECES -----------------------------
    //
    // The machine is a solid made of cells and the win is the moment
    // it stops being one. Every cell is thrown straight out from the
    // core — the thing that just took you — tumbling about an axis of
    // its own, and the throw is staggered by DISTANCE FROM THE CORE so
    // the break travels outward as a front rather than happening to
    // the whole object at once.
    //
    // It is the eversion's machinery pointed somewhere else, which is
    // the argument for having built it per-cell in the first place:
    // the board was already made of individually addressable cubes,
    // so the explosion is four uniforms and nothing on the CPU.
    //
    // THE SCHEDULE, at the shipped stagger of 0.55. The cell on the
    // core leaves immediately and the eight corners hold until the
    // throw is 35% done; halfway through, the corners are 53% of the
    // way out and the middle is 96%, which is the front. Every cell
    // lands exactly at _Burst = 1 whatever the spread, because the
    // front is scaled up by it and each cell subtracts its own share —
    // the same arithmetic the eversion above uses. The throw is 3.4
    // half-spans, which is about 1.4 boards from the centre: far
    // enough to clear the frame at the pull-back the exit asks for.
    //
    // The direction comes off the AUTHORED centre even when the board
    // is everted and the cell is standing at its mirror. A mirror
    // along the view axis changes only the sign of the component the
    // orthographic camera cannot see, so on screen the two answers are
    // the same throw — and the cell is still the cell it was authored
    // as, which is the answer that means something.
    float gone = 0.0;
    if (_Burst > 1e-5)
    {
        float far = max(1e-4, 1.7320508 * _HalfSpan);   // centre to corner
        float r = saturate(length(v.centre) / far);
        float k = saturate(_Burst * (1.0 + _BurstSpread) - r * _BurstSpread);

        // FAST OUT, THEN COASTING — the opposite ease to everything
        // else in this game, because this is the only thing that is
        // thrown rather than moved.
        float u = 1.0 - k;
        float e = 1.0 - u * u * u;

        // A cell sitting on the core has no direction of its own, so
        // it takes the one that is always true: it comes at you.
        float len = length(v.centre);
        float3 dir = lerp(float3(0, 0, -1), v.centre / max(1e-4, len),
                          saturate(len / max(1e-4, 0.5 * _HalfSpan)));

        // an axis that differs per cell, so the debris is a cloud of
        // separate objects rather than a set of parallel cards
        float3 tum = normalize(v.centre * float3(0.71, -1.03, 0.44)
                               + float3(0.31, 0.17, 0.93));
        float3 rel = local - eCentre;
        float ang = 6.2831853 * _BurstSpin * e;
        rel = Turn(rel, tum, ang);
        nrm = Turn(nrm, tum, ang);

        eCentre += dir * (_BurstThrow * _HalfSpan * e);
        local = eCentre + rel;
        gone = k;
    }
    o.gone = gone;

    o.pos = UnityObjectToClipPos(float4(local, 1));

    // Depth is measured from the CELL CENTRE, not the vertex, so that
    // at rest a front face reads exactly d/(N-1) — the same number the
    // flat 2D original prints — rather than half a step brighter.
    // Measured against the cube's own centre so it is independent of
    // how far away the camera happens to sit.
    // the ANIMATED centre, not the authored one — depth is drawn as
    // brightness, so a cell reading its old depth while it travels
    // would keep the near ramp all the way to the far side
    float centreZ = UnityObjectToViewPos(float4(eCentre, 1)).z;
    float originZ = UnityObjectToViewPos(float4(0, 0, 0, 1)).z;
    // Unity view space looks down -Z, so a LARGER z is nearer.
    o.depth01 = saturate((centreZ - originZ) / (2.0 * _HalfSpan) + 0.5);

    o.vnrm = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, nrm));
    o.uv = v.uv;
    o.reach = v.color;
    return o;
}

#endif
