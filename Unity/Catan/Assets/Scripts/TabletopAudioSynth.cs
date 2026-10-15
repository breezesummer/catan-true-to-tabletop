using System;

// 原创确定性程序音频；种子仅用于声纹，与游戏、UnityEngine.Random 和权威随机数隔离。
public static class TabletopAudioSynth
{
    public const int SampleRate = 24000;
    public const string RecipeVersion = "pastoral-tabletop-audio-v001";
    const double Tau = Math.PI * 2;

    sealed class Sound
    {
        internal readonly float[] Samples;
        uint random;
        internal Sound(double length, uint seed) { Samples = new float[(int)(length * SampleRate)]; random = seed + 1; }
        internal double Noise() { random ^= random << 13; random ^= random >> 17; random ^= random << 5; return random / (double)uint.MaxValue * 2 - 1; }
        internal void Tone(double at, double length, double frequency, double level, double decay = 7, double bend = 0)
        {
            int start = (int)(at * SampleRate), count = (int)(length * SampleRate);
            for (int i = 0; i < count && start + i < Samples.Length; i++)
            {
                double t = i / (double)SampleRate, u = t / length;
                double attack = Math.Min(1, t / .004), tail = Math.Min(1, (length - t) / .014);
                Samples[start + i] += (float)(Math.Sin(Tau * (frequency * t + bend * t * t * .5)) * level * attack * tail * Math.Exp(-u * decay));
            }
        }
        internal void NoiseBurst(double at, double length, double level, double filter, double decay = 6)
        {
            int start = (int)(at * SampleRate), count = (int)(length * SampleRate); double low = 0;
            for (int i = 0; i < count && start + i < Samples.Length; i++)
            {
                double t = i / (double)SampleRate;
                low += filter * (Noise() - low);
                Samples[start + i] += (float)(low * level * Math.Min(1, t / .003) * Math.Min(1, (length - t) / .014) * Math.Exp(-t / length * decay));
            }
        }
        internal void Wood(double at, double pitch, double volume = 1)
        {
            Tone(at, .14, pitch, .23 * volume, 11, -pitch * .6);
            Tone(at, .09, pitch * 2.76, .10 * volume, 12);
            NoiseBurst(at, .055, .27 * volume, .44, 11);
        }
        internal void Stone(double at, double pitch, double volume = 1)
        {
            Tone(at, .24, pitch, .29 * volume, 12, -pitch * .35);
            Tone(at, .12, pitch * 4.13, .09 * volume, 13);
            NoiseBurst(at, .21, .50 * volume, .28, 9);
        }
        internal void Metal(double at, double pitch, double volume = 1)
        {
            Tone(at, .40, pitch, .13 * volume, 6);
            Tone(at, .35, pitch * 2.71, .09 * volume, 7);
            Tone(at, .23, pitch * 4.14, .045 * volume, 7);
            NoiseBurst(at, .04, .24 * volume, .8, 10);
        }
        internal void Bell(double at, double pitch, double volume = 1)
        {
            Tone(at, .62, pitch, .16 * volume, 6);
            Tone(at, .38, pitch * 2, .065 * volume, 7);
            Tone(at, .27, pitch * 3.01, .025 * volume, 8);
        }
        internal void Paper(double at, double volume = 1)
        { NoiseBurst(at, .16, .31 * volume, .70, 3); NoiseBurst(at + .045, .14, .18 * volume, .37, 5); Wood(at + .10, 650, .26 * volume); }
        internal void Horn(double at, double pitch, double volume = 1)
        {
            Tone(at, .92, pitch, .17 * volume, 1.6, 6);
            Tone(at, .92, pitch * 2, .075 * volume, 2, 12);
            Tone(at, .83, pitch * 3, .025 * volume, 3, 18);
        }
        internal float[] Finish()
        {
            double peak = 0;
            for (int i = 0; i < Samples.Length; i++) peak = Math.Max(peak, Math.Abs(Samples[i]));
            double gain = peak > .70 ? .70 / peak : 1;
            int fade = Math.Min(120, Samples.Length / 2);
            for (int i = 0; i < Samples.Length; i++)
                Samples[i] = (float)(Samples[i] * gain * Math.Min(1, Math.Min(i, Samples.Length - 1 - i) / (double)fade));
            return Samples;
        }
    }

    public static float[] Render(TabletopAudioCue cue)
    {
        double length = cue == TabletopAudioCue.Victory ? 2.5 : cue == TabletopAudioCue.BarbarianInvasion ? 1.8 :
            cue == TabletopAudioCue.BuildingCollapse ? 1.25 : cue == TabletopAudioCue.DiceRoll ? .90 :
            cue == TabletopAudioCue.PirateMove ? 1.15 : cue == TabletopAudioCue.UiClick ? .15 :
            cue == TabletopAudioCue.UiBack ? .20 : 1.1;
        var s = new Sound(length, (uint)cue * 74117 + 17);
        switch (cue)
        {
            case TabletopAudioCue.UiClick: s.Wood(0, 800, .64); s.Tone(0, .045, 1800, .035, 8); break;
            case TabletopAudioCue.UiBack: s.Wood(0, 410, .55); s.Tone(.035, .10, 280, .06, 8); break;
            case TabletopAudioCue.CardPick: s.Paper(0, .75); break;
            case TabletopAudioCue.ResourceCollect: s.Paper(0, .60); s.Wood(.07, 530, .48); s.Wood(.14, 710, .46); s.Bell(.19, 1175, .42); break;
            case TabletopAudioCue.DiceRoll:
                for (int i = 0; i < 10; i++) { double t = i * .043 + i * i * .003; s.Wood(t, 590 + i % 3 * 165, .73 - i * .035); s.NoiseBurst(t, .035, .18, .8); }
                s.Wood(.66, 460, .90); s.Wood(.705, 730, .77); break;
            case TabletopAudioCue.RoadBuild: s.Wood(0, 260, .86); s.Wood(.12, 350, .95); s.NoiseBurst(.03, .19, .20, .16); break;
            case TabletopAudioCue.SettlementBuild: s.Wood(0, 220); s.Wood(.13, 360, .85); s.Wood(.27, 480, .68); s.Bell(.32, 660, .43); break;
            case TabletopAudioCue.CityBuild: s.Stone(0, 120); s.Stone(.18, 190, .86); s.Metal(.31, 660, .60); s.Bell(.36, 990, .44); break;
            case TabletopAudioCue.WallBuild: s.Stone(0, 160); s.Stone(.13, 180, .85); s.Stone(.27, 210, .75); break;
            case TabletopAudioCue.ShipBuild: s.Wood(0, 230); s.Wood(.14, 380, .85); s.NoiseBurst(.24, .58, .55, .08, 3); s.Bell(.32, 790, .34); break;
            case TabletopAudioCue.ShipMove: s.NoiseBurst(0, .78, .52, .045, 2); s.Wood(.09, 320, .40); s.Tone(.16, .55, 175, .06, 4, 220); break;
            case TabletopAudioCue.PortPlace: s.Wood(0, 380, .70); s.Metal(.14, 1045, .70); s.NoiseBurst(.09, .48, .23, .08, 3); break;
            case TabletopAudioCue.TradeOffer: s.Paper(0, .6); s.Bell(.11, 784, .6); break;
            case TabletopAudioCue.TradeComplete: s.Paper(0, .85); s.Metal(.10, 1319, .75); s.Metal(.20, 1568, .6); break;
            case TabletopAudioCue.CardDraw: s.Paper(0); s.Tone(.14, .30, 880, .05, 6); break;
            case TabletopAudioCue.CardPlay: s.Paper(0, .90); s.Wood(.12, 690, .65); s.Bell(.16, 1046, .5); break;
            case TabletopAudioCue.RobberMove: s.Wood(0, 140, .74); s.Tone(.025, .48, 195, .13, 5, -85); s.NoiseBurst(.06, .30, .24, .08, 4); break;
            case TabletopAudioCue.PirateMove: s.Horn(0, 139, .74); s.NoiseBurst(.14, .74, .53, .055, 3); break;
            case TabletopAudioCue.KnightRecruit: s.Metal(0, 520, .95); s.Wood(.13, 240, .70); break;
            case TabletopAudioCue.KnightPromote: s.Metal(0, 660, .7); s.Metal(.13, 830, .65); s.Bell(.26, 1109, .67); break;
            case TabletopAudioCue.KnightActivate: s.Metal(0, 940, .9); s.Wood(.10, 210, .6); break;
            case TabletopAudioCue.KnightMove:
                for (int i = 0; i < 4; i++) { s.Wood(i * .12, 200 + i % 2 * 45, .70); s.Wood(i * .12 + .045, 350, .38); } break;
            case TabletopAudioCue.KnightCombat: s.NoiseBurst(0, .23, .62, .8); s.Metal(.04, 1140); s.Stone(.18, 130, .8); s.Metal(.24, 850, .65); break;
            case TabletopAudioCue.BarbarianAdvance: s.Tone(0, .45, 105, .27, 8, -70); s.Wood(.16, 190, .66); s.Metal(.19, 390, .32); break;
            case TabletopAudioCue.BarbarianInvasion: s.Horn(.06, 110); s.Horn(.27, 164.8, .66); s.Stone(0, 65, 1.15); s.Stone(.42, 83, .85); s.Stone(.80, 73, .72); break;
            case TabletopAudioCue.BuildingCollapse:
                s.NoiseBurst(0, .96, .95, .19, 3);
                for (int i = 0; i < 7; i++) s.Stone(i * .095, 72 + i * 16, .9 - i * .09);
                s.Wood(.15, 130, .85); s.Wood(.44, 185, .6); break;
            case TabletopAudioCue.FacilityUpgrade: s.Wood(0, 340, .65); s.Metal(.08, 660, .45); s.Bell(.16, 880, .70); s.Bell(.30, 1109, .65); s.Bell(.44, 1319, .57); break;
            case TabletopAudioCue.WonderBuild: s.Stone(0, 150, .8); s.Bell(.14, 523, .65); s.Bell(.26, 784, .7); s.Bell(.40, 1046, .7); break;
            case TabletopAudioCue.TurnEnd: s.Wood(0, 460, .64); s.Bell(.10, 587, .52); break;
            case TabletopAudioCue.YourTurn: s.Bell(0, 784, .82); s.Bell(.18, 1046, .75); break;
            case TabletopAudioCue.Victory:
                double[] notes = { 523.25, 659.25, 783.99, 1046.5, 1318.5 };
                for (int i = 0; i < notes.Length; i++) s.Bell(i * .17, notes[i], .95);
                s.Horn(.78, 261.63, .48); s.Horn(.78, 329.63, .39); s.Horn(.78, 392, .36); break;
            case TabletopAudioCue.Decision: s.Wood(0, 610, .6); s.Bell(.09, 880, .30); break;
            case TabletopAudioCue.Error: s.Wood(0, 250, .65); s.Wood(.11, 190, .55); break;
            case TabletopAudioCue.Save: s.Paper(0, .55); s.Bell(.16, 987.77, .52); break;
            case TabletopAudioCue.Network: s.Bell(0, 659.25, .53); s.Bell(.13, 783.99, .52); break;
            default: throw new ArgumentOutOfRangeException(nameof(cue));
        }
        return s.Finish();
    }

    public static float[] RenderAmbience()
    {
        const double length = 12;
        var s = new Sound(length, 935701); double low = 0, breeze = 0;
        for (int i = 0; i < s.Samples.Length; i++)
        {
            double t = i / (double)SampleRate;
            low += .017 * (s.Noise() - low); breeze += .06 * (s.Noise() - breeze);
            // 低海浪与草地风保持低音量，周期包络在循环接缝处静音。
            double edge = Math.Min(1, Math.Min(t, length - t) / .6);
            s.Samples[i] = (float)((low * (.40 + .18 * Math.Sin(Tau * t / 4)) + breeze * .09) * edge);
        }
        for (int i = 0; i < 3; i++)
        { double at = 1.4 + i * 3.4; s.Tone(at, .13, 2300 + i * 170, .030, 2, 5500); s.Tone(at + .22, .19, 2600 + i * 130, .025, 3, -2800); }
        return s.Finish();
    }
}
