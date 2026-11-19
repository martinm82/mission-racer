using System;
using System.Collections.Generic;
using UnityEngine;

namespace MissionRacer.Localization
{
    public enum AppLanguage
    {
        English, // en
        German,  // de
        Polish   // pl
    }

    public class LocalizationManager : MonoBehaviour
    {
        public static LocalizationManager Instance { get; private set; }

        [SerializeField] private AppLanguage currentLanguage = AppLanguage.English;

        public AppLanguage CurrentLanguage => currentLanguage;
        public event Action<AppLanguage> OnLanguageChanged;

        private readonly Dictionary<string, Dictionary<AppLanguage, string>> _strings = new Dictionary<string, Dictionary<AppLanguage, string>>
        {
            // UI Headers & Tabs
            { "app_title", new Dictionary<AppLanguage, string> {
                { AppLanguage.English, "MISSION RACER" },
                { AppLanguage.German, "MISSION RACER" },
                { AppLanguage.Polish, "MISSION RACER" }
            }},
            { "tab_license", new Dictionary<AppLanguage, string> {
                { AppLanguage.English, "Racing License" },
                { AppLanguage.German, "Rennlizenz" },
                { AppLanguage.Polish, "Karta Kierowcy" }
            }},
            { "tab_missions", new Dictionary<AppLanguage, string> {
                { AppLanguage.English, "Missions (Pit Stop)" },
                { AppLanguage.German, "Missionen (Boxenstopp)" },
                { AppLanguage.Polish, "Misje (Pit Stop)" }
            }},
            { "tab_garage", new Dictionary<AppLanguage, string> {
                { AppLanguage.English, "Garage" },
                { AppLanguage.German, "Werkstatt / Garage" },
                { AppLanguage.Polish, "Garaż" }
            }},
            { "tab_race", new Dictionary<AppLanguage, string> {
                { AppLanguage.English, "Start Race" },
                { AppLanguage.German, "Rennen starten" },
                { AppLanguage.Polish, "Start Wyścigu" }
            }},

            // Stats
            { "fuel_label", new Dictionary<AppLanguage, string> {
                { AppLanguage.English, "Racing Fuel" },
                { AppLanguage.German, "Renn-Treibstoff" },
                { AppLanguage.Polish, "Paliwo Wyścigowe" }
            }},
            { "coins_label", new Dictionary<AppLanguage, string> {
                { AppLanguage.English, "Garage Coins" },
                { AppLanguage.German, "Werkstatt-Münzen" },
                { AppLanguage.Polish, "Monety Garażowe" }
            }},

            // Actions
            { "btn_stamp", new Dictionary<AppLanguage, string> {
                { AppLanguage.English, "Stamp License" },
                { AppLanguage.German, "Lizenz abstempeln" },
                { AppLanguage.Polish, "Podbij Licencję" }
            }},
            { "nfc_scan_prompt", new Dictionary<AppLanguage, string> {
                { AppLanguage.English, "Hold NFC License to back of phone..." },
                { AppLanguage.German, "Halte die Rennkarte an das Telefon..." },
                { AppLanguage.Polish, "Przyłóż kartę kierowcy do telefonu..." }
            }},

            // Missions
            { "mission_math", new Dictionary<AppLanguage, string> {
                { AppLanguage.English, "Math Practice (20 min)" },
                { AppLanguage.German, "Mathe-Hausaufgaben (20 Min)" },
                { AppLanguage.Polish, "Zadania z Matematyki (20 min)" }
            }},
            { "mission_reading", new Dictionary<AppLanguage, string> {
                { AppLanguage.English, "Reading Time (20 min)" },
                { AppLanguage.German, "Lesestunde (20 Min)" },
                { AppLanguage.Polish, "Czas na Czytanie (20 min)" }
            }},
            { "mission_room", new Dictionary<AppLanguage, string> {
                { AppLanguage.English, "Clean Bedroom & Desk" },
                { AppLanguage.German, "Zimmer & Schreibtisch aufräumen" },
                { AppLanguage.Polish, "Sprzątanie Pokoju & Biurka" }
            }}
        };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Auto-detect system language
            DetectSystemLanguage();
        }

        private void DetectSystemLanguage()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.German:
                    currentLanguage = AppLanguage.German;
                    break;
                case SystemLanguage.Polish:
                    currentLanguage = AppLanguage.Polish;
                    break;
                default:
                    currentLanguage = AppLanguage.English;
                    break;
            }
        }

        public void SetLanguage(AppLanguage language)
        {
            currentLanguage = language;
            OnLanguageChanged?.Invoke(currentLanguage);
        }

        public string Get(string key)
        {
            if (_strings.TryGetValue(key, out var langDict))
            {
                if (langDict.TryGetValue(currentLanguage, out var translated))
                {
                    return translated;
                }
                return langDict[AppLanguage.English]; // Fallback
            }
            return key;
        }
    }
}
