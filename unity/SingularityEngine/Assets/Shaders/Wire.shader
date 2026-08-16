// THE MACHINE AS A DRAWING.
//
// A held MATRIX used to THIN the board: the fill went translucent and the far
// side of each cell came up as edges. What it could not do was show you what was
// BEHIND — the depth buffer performs the projection, so every cell in a column
// but the nearest was gone before any fragment shader had an opinion — and it
// could not draw a cell that had no faces. CubeMesh.Build meshes away every face
// with something next to it, which is right, and it means a cell buried in the
// lattice has no vertices at all and a VOID has none by definition.
//
// So the schematic is its own geometry: one wire box per cell for every cell in
// the volume, empty ones included, drawn additively with no depth test at all.
// See CubeMesh.BuildWire for why the space matters as much as the material.
//
// IT SHARES THE SURFACE'S VERTEX STAGE, through Cell.cginc, and it has to: the
// arrival wave, the eversion and the exit burst all move cells individually, and
// a wire that did not travel with the cell it belongs to would come off it the
// moment anything moved.
//
// THE PROJECTION IS NOT BEING BROKEN. This draws nothing but lines, and a line is
// not a surface. Singularity/Cell still decides what the board IS and still obeys
// the depth buffer to the letter; this only says where the machine has material.
// That is the question a held MATRIX is asking, and the only one it can answer
// without lying about the rule the whole game is built on.
Shader "Singularity/Wire"
{
    Properties
    {
        // The three classes, which are the three answers: nothing here, something
        // here, something here you can stand on.
        _ColGrid    ("Empty",   Color) = (0.50, 0.60, 0.85, 1)
        _ColLattice ("Lattice", Color) = (0.50, 0.60, 0.85, 1)
        _ColTrace   ("Trace",   Color) = (0.62, 0.91, 1.00, 1)

        _GainGrid    ("Empty strength",   Range(0,1)) = 0.045
        _GainLattice ("Lattice strength", Range(0,1)) = 0.14
        _GainTrace   ("Trace strength",   Range(0,1)) = 0.40

        _WireFar  ("Strength at the far side", Range(0,1)) = 0.26
        _Peek     ("Matrix", Range(0,1)) = 0

        // shared with Singularity/Cell through Cell.cginc
        _HalfSpan ("Half span of cell centres", Float) = 1
        _Gutter   ("Gutter", Range(0,0.3)) = 0.055
        _Round    ("Corner radius", Range(0,0.5)) = 0.09
        _Edge     ("Edge inset", Range(0,0.5)) = 0.06
        _EdgeLift ("Edge brightness", Range(0,2)) = 0.55
        _Facing   ("Side-face darkening", Range(0,1)) = 0.42
        _Dim      ("Dim", Range(0,2)) = 1
        _Unlit    ("Unreachable dimming", Range(0,1)) = 0.46
        _UnlitSat ("Unreachable colour left", Range(0,1)) = 0.30
        _Reveal   ("Reveal front", Float) = 99
        _ColFar   ("Colour, far",  Color) = (0,0,0,1)
        _ColNear  ("Colour, near", Color) = (0,0,0,1)
        _WireCol  ("Wire colour", Color) = (1,1,1,1)
        _Wave     ("Transition front", Float) = 99
        _WaveDir  ("Transition direction", Float) = 1
        _WaveSoft ("Transition softness", Float) = 1.6
        _Evert     ("Eversion", Range(0,1)) = 0
        _EvertAxis ("Eversion axis, object space", Vector) = (0,0,1,0)
        _EvertSpin ("Eversion spin axis, object space", Vector) = (1,0,0,0)
        _EvertSpread ("Eversion stagger", Range(0,1)) = 0.75
        _Burst       ("Burst", Range(0,1)) = 0
        _BurstThrow  ("Burst throw, in half-spans", Float) = 3.4
        _BurstSpread ("Burst stagger", Range(0,1)) = 0.55
        _BurstSpin   ("Burst tumble, in turns", Float) = 1.35
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Geometry+10" }
        LOD 100

        Pass
        {
            Blend One One
            BlendOp Add
            ZWrite Off
            ZTest Always
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragWire
            #include "Cell.cginc"

            fixed4 _ColGrid, _ColLattice, _ColTrace;
            float _GainGrid, _GainLattice, _GainTrace;

            fixed4 fragWire(v2f i) : SV_Target
            {
                if (_Peek < 0.02) discard;

                // The class rode in on ALPHA, because the other three channels are
                // rewritten by the reach pass on every settle and this one is a
                // property of the cube rather than of the moment.
                float cls = i.reach.a;

                fixed3 col = cls < 0.25 ? _ColGrid.rgb
                           : cls < 0.75 ? _ColLattice.rgb
                                        : _ColTrace.rgb;
                float gain = cls < 0.25 ? _GainGrid
                           : cls < 0.75 ? _GainLattice
                                        : _GainTrace;

                // FAR IS FAINT. Without this every wire in the solid arrives at the
                // same strength and the picture has no inside — the eye is handed a
                // flat tangle instead of a depth. It is the same ramp the surface
                // uses, doing the same job with light instead of value.
                float fade = lerp(_WireFar, 1.0, i.depth01);

                // If the board is thrown while somebody is holding, the wire leaves
                // with the cell it is drawn on.
                float leaving = 1.0 - smoothstep(0.55, 1.0, i.gone);

                // NOT _Dim. That is the surface being taken away as the hold comes
                // on, and applying it here would dim the wire by exactly the amount
                // the wire exists to replace.
                return fixed4(col * (gain * fade * _Peek * leaving), 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
