#ifndef CATAN_DANGER_HEX_INCLUDED
#define CATAN_DANGER_HEX_INCLUDED
float _CatanMapMarksHidden;

// 内缘的朱红细线缓慢明灭，地形印刷、海浪和棋子仍可辨认。
half3 DangerHex(half3 base, float2 delta)
{
    float2 d = abs(delta);
    float edge = .034641016 - max(d.x, dot(d,float2(.5,.8660254)));
    if (edge <= 0) return base;
    float inset = abs(edge - .0030);
    float aa = max(.00018, fwidth(edge) * .6);
    float stroke = 1 - smoothstep(.00040,.00085 + aa,inset);
    float halo = exp2(-inset * 510);
    float pulse = .88 + .12 * sin(_Time.y * 2.8);
    half3 red = half3(.93,.038,.025);
    base = lerp(base,half3(.58,.024,.018),.10 * pulse);
    return lerp(base,red,saturate((.96 * stroke + .31 * halo) * pulse));
}
#endif
