using System;
using System.Collections.Generic;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// 对质台词池 —— 数据来自 0.8.0 发布包原文（3 位卖方顾客 × 4 类 × 10 条）。
    /// `AlreadyDone` 已按用户要求删除。
    /// </summary>
    internal static class FigurineConfrontLines
    {
        public enum Kind
        {
            Correct = 0,
            WrongPart = 1,
            NotFake = 2,
            NoPick = 3,
            AlreadyDone = 4,
        }

        public sealed class Pool
        {
            public string[] Correct;
            public string[] WrongPart;
            public string[] NotFake;
            public string[] NoPick;
            public string[] AlreadyDone;

            public string[] Of(Kind k)
            {
                switch (k)
                {
                    case Kind.Correct: return Correct;
                    case Kind.WrongPart: return WrongPart;
                    case Kind.NotFake: return NotFake;
                    case Kind.NoPick: return NoPick;
                    case Kind.AlreadyDone: return AlreadyDone;
                    default: return null;
                }
            }

            public int Total()
            {
                return (Correct?.Length ?? 0) + (WrongPart?.Length ?? 0) + (NotFake?.Length ?? 0)
                     + (NoPick?.Length ?? 0) + (AlreadyDone?.Length ?? 0);
            }
        }

        private static readonly Dictionary<string, Pool> Table = new Dictionary<string, Pool>(StringComparer.Ordinal)
        {
            ["figurine_collector_upper_sell"] = new Pool
            {
                Correct = new string[10]
                {
                    "……{部件}。你看得比我细。",
                    "唉。我早该把它挑出来的。",
                    "你眼力好。这一件我确实看走眼了。",
                    "被你发现了。我收到它的时候也犹豫过。",
                    "……是{部件}。我认。",
                    "我收了一辈子东西，还是栽在这件上。",
                    "你说得对。我没什么好辩的。",
                    "看来我这双眼睛该歇了。",
                    "这件的{部件}，我其实一直在回避看它。",
                    "好眼力。这件我留着也是自欺欺人。"
                },
                WrongPart = new string[10]
                {
                    "……{部件}是好的。你再看看。",
                    "不是那里。我仔细瞧过的。",
                    "你指错了。这件确实有问题，但不是{部件}。",
                    "我承认这件有问题，可你说的那处没问题。",
                    "再看一遍吧。你说的那个地方是齐的。",
                    "错了。你要是只看这里，看不出什么。",
                    "不是这处。我收东西还是有点眼力的。",
                    "你说的不对。这件我起码验过三遍。",
                    "换个地方看。你说的那处好得很。",
                    "我年纪大了，但还没糊涂到这地步。"
                },
                NotFake = new string[10]
                {
                    "这是正品。我收它的时候有据可查。",
                    "你冤枉我了。这件一点问题都没有。",
                    "我卖东西从不掺假。这件是我的旧藏。",
                    "你把真东西说成假的，这话传出去我怎么做人。",
                    "这件干干净净。你凭什么说是假的？",
                    "我摆弄这些东西几十年了。这是真的。",
                    "你可以再看，但它是正品。",
                    "别急着下结论。这件我敢拿命担保。",
                    "你这话说得我心里不舒服。它是真的。",
                    "我这辈子没收过假货，也没卖过。"
                },
                NoPick = new string[10]
                {
                    "你什么都没指，就问我这个？",
                    "你还没看出问题，就先开口了。",
                    "什么都不说，是没看出来吧。",
                    "你倒是先看看，再跟我说话。",
                    "空口无凭。你指出来我再答。",
                    "……你这样我可不知道该怎么答。",
                    "你要是指不出哪里不对，我就当它是好的。",
                    "看了半天，一句话都没有？",
                    "你连哪里有问题都说不清。",
                    "我等你开口等了半天。"
                },
                AlreadyDone = new string[10]
                {
                    "这事你说过了。别再翻。",
                    "我们已经谈过这件。到此为止。",
                    "同一件东西，你要问几遍？",
                    "我不喜欢反复。这页翻过去了。",
                    "你又说一遍，结果还是一样。",
                    "这件事我认了，你也别再提。",
                    "话说一次就够。",
                    "你再问，我就把东西收回去。",
                    "旧事重提没意思。",
                    "我耳朵不太好，但记性还行。"
                },
            },
            ["figurine_enthusiast_tourist"] = new Pool
            {
                Correct = new string[10]
                {
                    "哇，这你都能看出来？",
                    "诶——是{部件}对吧。我都没留意。",
                    "被你说中了。哈哈，我认。",
                    "你眼神也太好了吧。",
                    "……好吧，{部件}。我不狡辩了。",
                    "行行行，是我不仔细。",
                    "我收的时候光顾着高兴了。",
                    "啧，被逮到了。",
                    "你这么一说，我也觉得不太对。",
                    "认了认了，是{部件}。"
                },
                WrongPart = new string[10]
                {
                    "诶？不是那里吧。",
                    "你再看看，{部件}是好的。",
                    "唔……它确实有点问题，可不是你说的那处。",
                    "我仔细看过，{部件}没缺。",
                    "你指错啦，虽然它也不太行。",
                    "不对不对，再看一眼。",
                    "哈哈，这回是你眼花了吧。",
                    "我不敢说它全好，但那处是齐的。",
                    "再看仔细点——不是这里。",
                    "我虽然不懂行，{部件}还是看得出来的。"
                },
                NotFake = new string[10]
                {
                    "诶？这是真的呀。",
                    "你别吓我，它明明好好的。",
                    "我看看……真没缺东西啊。",
                    "冤枉我了，这件没问题。",
                    "哇，你这么说我很受伤欸。",
                    "我自己收的，怎么会是假的。",
                    "你再看看，是正品啦。",
                    "……你认真的？",
                    "我虽然粗心，也不至于带假货来。",
                    "这件的成色我看得出来，是真的。"
                },
                NoPick = new string[10]
                {
                    "诶？你什么也没指啊。",
                    "你不指一下，我怎么知道你说哪里。",
                    "所以……是哪件有问题？",
                    "你就这么问我，我有点慌。",
                    "什么都没有，你让我说什么好。",
                    "先挑一处吧，不然我答不上来。",
                    "哈哈，你这是在考我吗？",
                    "不说具体哪处，我可不服。",
                    "你倒是先看看呀。",
                    "我等你指出来呢。"
                },
                AlreadyDone = new string[10]
                {
                    "这件我们说过啦。",
                    "诶，不是刚看过吗？",
                    "又来？我已经认过了。",
                    "翻篇了吧，说点别的。",
                    "你问第二遍我也不改口。",
                    "同一件问两次，你很执着欸。",
                    "我知道啦，别念了。",
                    "这事儿不是清楚了吗。",
                    "再问我还是那句话。",
                    "你这是在逗我吗？"
                },
            },
            ["figurine_pickpocket_1"] = new Pool
            {
                Correct = new string[10]
                {
                    "嘿。{部件}你都能看出来。",
                    "行家啊。我服。",
                    "我手快，眼睛可没你毒。",
                    "被你抓到了。这单我认。",
                    "……我顺它的时候没细看。",
                    "你比失主还仔细。",
                    "行。便宜你了。",
                    "我认。但我还是赚了。",
                    "眼力不错。下回我注意。",
                    "嘘。别声张。这事就算了。"
                },
                WrongPart = new string[10]
                {
                    "不是那处！我摸过的。",
                    "{部件}好着呢。你看偏了。",
                    "你指错了。手快的人眼睛也不差。",
                    "不对。你再看看。",
                    "不是那里。我拿的时候检查过。",
                    "你指的地方我翻了三遍。",
                    "你指错地方了。",
                    "这处我翻过。",
                    "我认它是假的，但不是这处。",
                    "你看走眼了。"
                },
                NotFake = new string[10]
                {
                    "这件是真的！这东西我可造不出来。",
                    "你别冤枉我。我顺来的就是原样。",
                    "我手快，但没换过里面的东西。",
                    "真的。你自己看。",
                    "手快的人眼也不瞎。",
                    "我认偷，不认造假。",
                    "这件我拿的时候就长这样。",
                    "你要是不要，我拿去别家。",
                    "干净的东西。我敢让你看。",
                    "别把两件事混一起。"
                },
                NoPick = new string[10]
                {
                    "你倒是说啊。我时间紧。",
                    "光看不说，你磨什么呢。",
                    "什么都不指？那我走了。",
                    "你快点。人堆里还等着我。",
                    "别吊着我。",
                    "看够了没有。",
                    "给句话。",
                    "我手都举酸了。",
                    "你说话。",
                    "别逮着一件问。"
                },
                AlreadyDone = new string[10]
                {
                    "这话你说过了。",
                    "同一件，问两遍也白问。",
                    "我手快，记性也不差。",
                    "你磨叽什么呢。",
                    "再问我就溜了。",
                    "这单早清了。",
                    "我耳朵好得很。别再喊。",
                    "你烦不烦。",
                    "往前看吧，别回头。",
                    "我说完了。闪了。"
                },
            },
        };

        private static bool _haiziRegistered;

        /// <summary>海子的对质 5 类（他当卖方被指认时用）。</summary>
        public static void EnsureHaizi()
        {
            try
            {
                if (_haiziRegistered) return;
                _haiziRegistered = true;
                if (Table.ContainsKey(Haizi.ClientId)) return;
                var p = new Pool();
                HaiziLines.FillConfront(p);
                Table[Haizi.ClientId] = p;
            }
            catch (Exception e) { Safe.Warn("FigurineConfrontLines.EnsureHaizi", e); }
        }
        public static string Pick(string customerId, Kind kind, string part)
        {
            try
            {
                if (string.IsNullOrEmpty(customerId)) return null;
                if (customerId == Haizi.ClientId) EnsureHaizi();
                Pool p;
                if (!Table.TryGetValue(customerId, out p) || p == null) return null;
                var arr = p.Of(kind);
                if (arr == null || arr.Length == 0) return null;
                string line = arr[UnityEngine.Random.Range(0, arr.Length)];
                if (string.IsNullOrEmpty(line)) return null;
                if (!string.IsNullOrEmpty(part)) line = line.Replace("{部件}", part);
                return line;
            }
            catch (Exception e) { Safe.Warn("FigurineConfrontLines.Pick", e); return null; }
        }

        public static string NameOf(Kind k)
        {
            switch (k)
            {
                case Kind.Correct: return "指认正确";
                case Kind.WrongPart: return "指认的部件不对";
                case Kind.NotFake: return "这是正品";
                case Kind.NoPick: return "没有勾选";
                case Kind.AlreadyDone: return "已对质过";
                default: return "";
            }
        }
    }
}
