using System;
using MochiV2.Core.Behavior;
using Serilog;

namespace MochiV2.Core.Services
{
    /// <summary>
    /// Daily quote/fact service. Post-MVP Phase G-1.
    /// Shows an inspirational quote once per morning (8-10am) via speech bubble.
    /// </summary>
    public sealed class DailyQuoteService
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(DailyQuoteService));

        private readonly ITimeProvider _timeProvider;
        private readonly SpeechBubbleService _speechBubble;
        private DateTime _lastQuoteDate = DateTime.MinValue;

        private static readonly string[] Quotes =
        {
            "Hari yang baik dimulai dengan senyum! 😊",
            "Kerja keras tidak akan mengkhianati kamu 💪",
            "Istirahat juga produktivitas 🌿",
            "Satu langkah kecil masih kemajuan 🐾",
            "Kucing tahu kapan harus tidur — ikuti kucing 😴",
            "Fokus pada progress, bukan kesempurnaan ✨",
            "Hari ini penuh kemungkinan baru 🌅",
            "Jangan lupa makan! 🍱",
            "Tarik napas dalam — kamu bisa 💚",
            "Kucing tidak khawatir masa depan, mereka tidur 🐱",
        };

        private static readonly string[] Facts =
        {
            "Kucing tidur 12-16 jam sehari 😴",
            "Kucing bisa mendengar suara yang terlalu tinggi untuk manusia 👂",
            "Setiap kucing memiliki pola hidung unik 🐾",
            "Kucing mengeong hanya untuk berkomunikasi dengan manusia 🗣️",
            "Kucing menggerakkan telinga 32 otot per telinga 👂",
            "Kucing bisa melompat 6x tinggi badannya 🐱",
            "Kucing menghabiskan 70% hidupnya untuk tidur 💤",
        };

        public bool Enabled { get; set; } = true;

        public DailyQuoteService(ITimeProvider timeProvider, SpeechBubbleService speechBubble)
        {
            _timeProvider = timeProvider;
            _speechBubble = speechBubble;
        }

        /// <summary>Tick — call every frame. Shows quote once per day in the morning.</summary>
        public void Tick()
        {
            if (!Enabled) return;
            var now = DateTime.Now;

            // Only show between 8am-10am, once per day
            if (now.Hour < 8 || now.Hour >= 10) return;
            if (now.Date == _lastQuoteDate) return;

            _lastQuoteDate = now.Date;
            var rng = new Random();
            string message;
            if (rng.NextDouble() < 0.5)
                message = Quotes[rng.Next(Quotes.Length)];
            else
                message = Facts[rng.Next(Facts.Length)];

            _speechBubble.Show(message, 8.0);
            Logger.Information("Daily quote shown: {Message}", message);
        }

        /// <summary>Force show a quote now (for testing).</summary>
        public string ShowRandomQuote()
        {
            var rng = new Random();
            string message = Quotes[rng.Next(Quotes.Length)];
            _speechBubble.Show(message, 8.0);
            return message;
        }
    }
}