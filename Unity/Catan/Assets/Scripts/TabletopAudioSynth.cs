using System;

// 原创确定性程序音频；种子仅用于声纹，与游戏、UnityEngine.Random 和权威随机数隔离。
public static class TabletopAudioSynth
{
    public const int SampleRate = 24000;
    public const string RecipeVersion = "pastoral-tabletop-audio-v002";
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
        internal void Anvil(double at, double volume = 1)
        {
            Metal(at, 710, volume); Tone(at, .74, 1471, .105 * volume, 5);
            Tone(at, .62, 2347, .065 * volume, 6); Stone(at, 130, .40 * volume);
        }
        internal void Gallop(double at, double volume = 1)
        {
            // 四拍蹄声与渐近低频尘土构成骑兵，不用 UI 铃音替代行动。
            for (int i = 0; i < 5; i++)
            {
                double t = at + i * .22; double gain = volume * (.55 + .08 * i);
                Wood(t, 135, gain); Wood(t + .065, 245, gain * .8);
                Stone(t + .115, 96, gain * .55); Wood(t + .155, 190, gain * .60);
                NoiseBurst(t, .22, .12 * gain, .075, 2);
            }
        }
        internal void Crowd(double at, double volume = 1)
        {
            // 原创六声部 /hu/ 群体吼声：低基频、口腔共振与呼气，无任何商业游戏采样。
            for (int member = 0; member < 6; member++)
            {
                double start = at + member * .017, f0 = 82 + member * 7.7, phase = member * .71;
                int count = (int)(.73 * SampleRate), begin = (int)(start * SampleRate);
                for (int i = 0; i < count && begin + i < Samples.Length; i++)
                {
                    double t = i / (double)SampleRate;
                    double envelope = Math.Pow(Math.Sin(Math.PI * t / .73), 1.45);
                    double f = f0 * t - 12 * t * t;
                    double v = 0;
                    for (int h = 1; h <= 22; h++)
                    {
                        double hz = f0 * h;
                        double formant = .13 + Math.Exp(-Math.Pow((hz - 350) / 130, 2)) + .55 * Math.Exp(-Math.Pow((hz - 820) / 210, 2));
                        v += Math.Sin(Tau * f * h + phase) * formant / (h * .85 + 1);
                    }
                    Samples[begin + i] += (float)(v * envelope * .068 * volume);
                }
                NoiseBurst(start, .19, .18 * volume, .25, 2);
            }
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
        double length = cue == TabletopAudioCue.Victory ? 2.5 : cue == TabletopAudioCue.BarbarianInvasion ? 2.1 :
            cue == TabletopAudioCue.KnightCombat || cue == TabletopAudioCue.KnightMove ? 1.55 :
            cue == TabletopAudioCue.BuildingCollapse || cue == TabletopAudioCue.ShipBuild ? 1.65 :
            cue == TabletopAudioCue.TradeUpgrade || cue == TabletopAudioCue.PoliticsUpgrade || cue == TabletopAudioCue.ScienceUpgrade || cue == TabletopAudioCue.KnightPromote ? 1.6 :
            cue == TabletopAudioCue.BarbarianSail ? 2.2 : cue == TabletopAudioCue.DiceRoll ? .90 :
            cue == TabletopAudioCue.PirateMove ? 1.15 : cue == TabletopAudioCue.UiClick ? .15 :
            cue == TabletopAudioCue.UiBack ? .20 : 1.1;
        var s = new Sound(length, (uint)cue * 74117 + 17);
        switch (cue)
        {
            case TabletopAudioCue.UiClick: s.Wood(0, 620, .38); s.NoiseBurst(0, .035, .08, .6, 9); break;
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
            case TabletopAudioCue.ShipBuild:
                s.Wood(0, 165, .65); s.Tone(.13, .65, 66, .35, 5, -39);
                s.NoiseBurst(.14, 1.28, 1.3, .16, 3); s.NoiseBurst(.21, .72, .62, .52, 3);
                for (int i = 0; i < 9; i++) s.Tone(.29 + i * .10, .20, 220 + i * 31, .060, 5, -440);
                break;
            case TabletopAudioCue.ShipMove: s.NoiseBurst(0, .78, .52, .045, 2); s.Wood(.09, 320, .40); s.Tone(.16, .55, 175, .06, 4, 220); break;
            case TabletopAudioCue.PortPlace: s.Wood(0, 380, .70); s.Metal(.14, 1045, .70); s.NoiseBurst(.09, .48, .23, .08, 3); break;
            case TabletopAudioCue.TradeOffer: s.Metal(0, 1820, .72); s.Metal(.075, 2350, .58); s.Metal(.17, 2080, .46); break;
            case TabletopAudioCue.TradeComplete:
                for (int i = 0; i < 6; i++) s.Metal(i * .048 + i * i * .006, 1600 + i % 3 * 430, .84 - i * .08);
                s.Wood(.34, 520, .24); break;
            case TabletopAudioCue.CardDraw: s.Paper(0); s.Tone(.14, .30, 880, .05, 6); break;
            case TabletopAudioCue.CardPlay: s.NoiseBurst(0, .20, .82, .85, 2); s.NoiseBurst(.085, .11, .35, .35, 3); s.Wood(.14, 1180, .32); break;
            case TabletopAudioCue.RobberMove: s.Crowd(.025, .85); s.Wood(.01, 140, .45); break;
            case TabletopAudioCue.PirateMove: s.Horn(0, 139, .74); s.NoiseBurst(.14, .74, .53, .055, 3); break;
            case TabletopAudioCue.KnightRecruit: s.Metal(0, 520, .95); s.Wood(.13, 240, .70); break;
            case TabletopAudioCue.KnightPromote: s.Anvil(0, .86); s.Anvil(.32, .94); s.Anvil(.67, .70); break;
            case TabletopAudioCue.KnightActivate: s.Metal(0, 940, .9); s.Wood(.10, 210, .6); break;
            case TabletopAudioCue.KnightMove: s.Gallop(.02, .90); s.Metal(.75, 640, .20); break;
            case TabletopAudioCue.KnightCombat: s.Gallop(0, 1.08); s.Crowd(.39, .60); s.Metal(.97, 850, .36); break;
            case TabletopAudioCue.BarbarianAdvance: s.Crowd(0); s.Stone(.14, 70, .36); break;
            case TabletopAudioCue.BarbarianInvasion: s.Crowd(.03, 1.1); s.Crowd(.82, .70); s.Stone(.04, 65, .85); s.Horn(.91, 110, .47); break;
            case TabletopAudioCue.BarbarianSail: s.Horn(.08, 123.47, .90); s.Horn(.91, 98, .63); s.NoiseBurst(0, 2.1, .30, .035, 1); break;
            case TabletopAudioCue.BuildingCollapse:
                s.Crowd(.10, .85); s.NoiseBurst(0, 1.42, .95, .19, 3);
                for (int i = 0; i < 7; i++) s.Stone(i * .095, 72 + i * 16, .9 - i * .09);
                s.Wood(.15, 130, .85); s.Wood(.44, 185, .6); break;
            case TabletopAudioCue.FacilityUpgrade: s.Wood(0, 340, .65); s.Metal(.08, 660, .45); s.Bell(.16, 880, .70); s.Bell(.30, 1109, .65); s.Bell(.44, 1319, .57); break;
            case TabletopAudioCue.TradeUpgrade:
                for (int i = 0; i < 19; i++) { double at = i * .060; s.Wood(at, 430, .38); s.Metal(at + .024, 1060, .16); }
                s.Tone(.04, 1.25, 92, .055, .5); s.NoiseBurst(.02, 1.28, .20, .12, .5); break;
            case TabletopAudioCue.PoliticsUpgrade: s.Anvil(0, 1); s.Anvil(.39, .85); s.Anvil(.77, .72); break;
            case TabletopAudioCue.ScienceUpgrade:
                for (int i = 0; i < 9; i++) { s.NoiseBurst(i * .125, .085, .50, .58, 1.5); s.NoiseBurst(i * .125 + .06, .043, .26, .80, 2); }
                s.Paper(1.20, .52); break;
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

    public static float[] RenderAmbience() => RenderAmbience(0);

    public static float[] RenderAmbience(int theme)
    {
        if (theme < 0 || theme > 4) throw new ArgumentOutOfRangeException(nameof(theme));
        const double length = 12;
        var s = new Sound(length, 935701 + (uint)theme * 3917); double low = 0, breeze = 0, stream = 0;
        for (int i = 0; i < s.Samples.Length; i++)
        {
            double t = i / (double)SampleRate;
            low += .017 * (s.Noise() - low); breeze += .06 * (s.Noise() - breeze);
            stream += .24 * (s.Noise() - stream);
            double edge = Math.Min(1, Math.Min(t, length - t) / .6);
            double wave = .40 + .18 * Math.Sin(Tau * t / 4);
            double value = theme == 0 ? low * wave + breeze * .09 :
                theme == 1 ? low * .28 + breeze * (.14 + .10 * Math.Sin(Tau * t / 6)) :
                theme == 2 ? low * .18 + stream * (.20 + .04 * Math.Sin(Tau * t / 3)) :
                theme == 3 ? low * (.58 + .20 * Math.Sin(Tau * t / 6)) + breeze * .06 :
                low * .24 + breeze * .045;
            s.Samples[i] = (float)(value * edge);
        }
        if (theme < 3) for (int i = 0; i < 3; i++)
        { double at = 1.4 + i * 3.4; s.Tone(at, .13, 2300 + i * 170 + theme * 90, .030, 2, 5500); s.Tone(at + .22, .19, 2600 + i * 130, .025, 3, -2800); }
        if (theme == 2) for (int i = 0; i < 18; i++) s.Tone(.8 + i * .56, .11, 700 + i % 5 * 121, .010, 6, -1400);
        if (theme == 4) for (int i = 0; i < 32; i++) { double at = 1.0 + i * .29; s.Tone(at, .045, 3600, .018, 1); s.Tone(at + .07, .045, 3710, .013, 1); }
        return s.Finish();
    }

    public const int MusicSamples = 1280000; // 72 BPM，16 小节，53 又 1/3 秒。
    public static float[] RenderMusic()
    {
        // 原创旋律「海风归途」：尼龙拨弦、低弦与柔和长笛。渲染两遍后取第二遍，让首尾保留相同的尾音。
        const double beat = 5.0 / 6, period = MusicSamples / (double)SampleRate;
        var s = new Sound(period * 2 + 4, 744091);
        int[][] chords = { new[] { 48, 55, 60, 64 }, new[] { 45, 52, 57, 60 }, new[] { 41, 48, 53, 57 }, new[] { 43, 50, 55, 62 } };
        int[][] melody = {
            new[] { 67, 64, 62, 64 }, new[] { 60, 64, 67, 69 }, new[] { 65, 64, 60, 62 }, new[] { 62, 59, 62, 67 },
            new[] { 64, 67, 72, 67 }, new[] { 69, 67, 64, 60 }, new[] { 65, 69, 67, 65 }, new[] { 62, 64, 62, 59 },
            new[] { 60, 64, 67, 64 }, new[] { 60, 57, 64, 67 }, new[] { 65, 64, 60, 57 }, new[] { 62, 67, 62, 59 },
            new[] { 64, 67, 72, 71 }, new[] { 69, 67, 64, 60 }, new[] { 65, 64, 62, 60 }, new[] { 62, 59, 55, 59 }
        };
        for (int cycle = 0; cycle < 2; cycle++) for (int bar = 0; bar < 16; bar++)
        {
            double start = cycle * period + bar * beat * 4; int[] chord = chords[bar % 4];
            s.Tone(start, beat * 3.8, Frequency(chord[0] - 12), .065, 3);
            for (int step = 0; step < 8; step++)
            {
                int note = chord[new[] { 0, 1, 2, 3, 1, 2, 3, 2 }[step]];
                double at = start + step * beat * .5, frequency = Frequency(note);
                s.Tone(at, 2.0, frequency, .095, 5.7);
                s.Tone(at, 1.3, frequency * 2, .026, 6.0);
                s.Tone(at, .7, frequency * 3, .009, 7.0);
            }
            for (int n = 0; n < 4; n++)
            {
                double at = start + n * beat, f = Frequency(melody[bar][n]);
                // 留出呼吸：每四小节末省略最后一拍，让音乐能长期陪伴操作。
                if (bar % 4 == 3 && n == 3) continue;
                int begin = (int)(at * SampleRate), count = (int)(beat * .90 * SampleRate);
                for (int i = 0; i < count; i++)
                {
                    double t = i / (double)SampleRate, u = i / (double)count;
                    double env = Math.Pow(Math.Sin(Math.PI * u), .85);
                    double phase = Tau * f * t + .012 * Math.Sin(Tau * 4.3 * t);
                    s.Samples[begin + i] += (float)((Math.Sin(phase) + .13 * Math.Sin(phase * 2)) * env * .047);
                }
            }
        }
        var result = new float[MusicSamples]; Array.Copy(s.Samples, MusicSamples, result, 0, MusicSamples);
        return result;
    }
    static double Frequency(int midi) => 440 * Math.Pow(2, (midi - 69) / 12.0);
}
