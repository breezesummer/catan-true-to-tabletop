// 陆侧与水下使用同一个世界坐标分布，沙滩不会在岸线处换色。
float BeachAmount(float2 p)
{
    float region=sin(p.x*31+p.y*17)*.58+cos(p.y*43-p.x*13)*.42;
    return smoothstep(-.08,.40,region);
}
float ShoreVariation(float2 p)
{
    return sin(p.x*210+p.y*143)*.5+cos(p.y*313-p.x*117)*.25;
}

