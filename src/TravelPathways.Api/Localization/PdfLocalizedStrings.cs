using System.Globalization;

namespace TravelPathways.Api.Localization;

/// <summary>All user-visible PDF labels and template phrases (English + Indian languages).</summary>
public sealed class PdfLocalizedStrings
{
    public required string LanguageCode { get; init; }
    public CultureInfo Culture { get; init; } = CultureInfo.GetCultureInfo("en-IN");

    // Cover / title
    public string HolidayQuote { get; init; } = "Holiday Quote";
    public string TravelItinerary { get; init; } = "Travel Itinerary";

    // Greeting block
    public string DearClientSalutation { get; init; } = "Dear {{ClientName}},";
    public string ThankYouChoosingTour { get; init; } = "Thank you for choosing {{AgencyName}} for your tour.";
    public string GreetingBody { get; init; } =
        "We are delighted to be a part of your journey and look forward to providing you with a memorable travel experience filled with comfort, beauty, and unforgettable moments.";

    // Client strip & facts
    public string Guest { get; init; } = "Guest";
    public string Phone { get; init; } = "Phone";
    public string EmailLabel { get; init; } = "Email";
    public string PaxLabel { get; init; } = "Pax";
    public string MealPlan { get; init; } = "Meal plan";
    public string Trip { get; init; } = "Trip";
    public string Arrival { get; init; } = "Arrival";
    public string Departure { get; init; } = "Departure";
    public string Duration { get; init; } = "Duration";
    public string Destination { get; init; } = "Destination";
    public string Transfer { get; init; } = "Transfer";
    public string Transport { get; init; } = "Transport";
    public string Vehicle { get; init; } = "Vehicle";
    public string Route { get; init; } = "Route";
    public string PickUp { get; init; } = "Pick-up";
    public string DropOff { get; init; } = "Drop-off";
    public string DefaultDestination { get; init; } = "Jammu & Kashmir";

    // Pricing & sections
    public string QuoteSummary { get; init; } = "Quote summary";
    public string QuotedAmount { get; init; } = "Quoted amount";
    public string ItineraryOverview { get; init; } = "Itinerary overview";
    public string WhereYouStay { get; init; } = "Where you stay";
    public string Inclusions { get; init; } = "Inclusions";
    public string Exclusions { get; init; } = "Exclusions";
    public string TermsAndConditions { get; init; } = "Terms & conditions";
    public string Cancellation { get; init; } = "Cancellation";
    public string SupplementCosts { get; init; } = "Supplement costs";
    public string SupplementsAndNotes { get; init; } = "Supplements & notes";

    // Bank & payment
    public string BankDetails { get; init; } = "Bank details";
    public string PaymentQr { get; init; } = "Payment QR";
    public string PayOnline { get; init; } = "Pay online";
    public string AccountHolder { get; init; } = "Account holder";
    public string Bank { get; init; } = "Bank";
    public string AccountNumber { get; init; } = "Account number";
    public string AccountNoShort { get; init; } = "Account no.";
    public string Ifsc { get; init; } = "IFSC";

    // About / footer
    public string AboutUs { get; init; } = "About us";
    public string AboutUsLine1 { get; init; } =
        "<strong>{{AgencyName}}</strong> is committed to delivering thoughtful itineraries, reliable service, and enriching travel experiences.";
    public string AboutUsLine2 { get; init; } =
        "Our team works closely with guests to ensure comfort, clarity, and memorable moments from arrival to departure.";
    public string AboutUsThanks { get; init; } =
        "We thank you for your trust and look forward to being part of your journey.";
    public string LeadershipAndContact { get; init; } = "Leadership & contact";
    public string ManagingDirector { get; init; } = "Managing Director";
    public string SalesHead { get; init; } = "Sales Head";
    public string ContactLabel { get; init; } = "Contact";
    public string RegisteredOffice { get; init; } = "Registered office";
    public string Website { get; init; } = "Website";
    /// <summary>Fallback when tenant has no address (matches default template sample office).</summary>
    public string DefaultRegisteredOfficeAddressHtml { get; init; } =
        "3rd Floor, Asad Complex Sangrama, Sopore,<br />Jammu and Kashmir — 193201";

    // Generator-built fragments
    public string Hotel { get; init; } = "Hotel";
    public string Houseboat { get; init; } = "Houseboat";
    public string Night { get; init; } = "Night";
    public string Nights { get; init; } = "Nights";
    public string Day { get; init; } = "Day";
    public string Days { get; init; } = "Days";
    public string Rooms { get; init; } = "Rooms";
    public string ExtraBed { get; init; } = "Extra bed";
    public string Cnb { get; init; } = "CNB";
    public string Adults { get; init; } = "Adults";
    public string Children { get; init; } = "Children";
    public string DayActivities { get; init; } = "Day activities";
    public string HotelName { get; init; } = "Hotel name";
    public string HotelArea { get; init; } = "Hotel area";

    public string NightLabel(int count) => count == 1 ? Night : Nights;
    public string DayLabel(int count) => count == 1 ? Day : Days;

    public string DaysDurationLabel(int nights, int days) =>
        $"{nights} {NightLabel(nights)} / {days} {DayLabel(days)}";

    public string PaxSummary(int adults, int children) =>
        children > 0
            ? $"{adults} {Adults} + {children} {Children}"
            : $"{adults} {Adults}";

    public IReadOnlyList<(string English, string Localized)> GetTemplateReplacements()
    {
        var en = English();
        var pairs = new List<(string, string)>();

        void Add(string english, string localized)
        {
            if (!string.IsNullOrEmpty(english) && !string.Equals(english, localized, StringComparison.Ordinal))
                pairs.Add((english, localized));
        }

        Add(en.DearClientSalutation, DearClientSalutation);
        Add(en.ThankYouChoosingTour, ThankYouChoosingTour);
        Add(en.GreetingBody, GreetingBody);
        Add(en.Guest, Guest);
        Add(en.Phone, Phone);
        Add(en.EmailLabel, EmailLabel);
        Add(en.PaxLabel, PaxLabel);
        Add(en.MealPlan, MealPlan);
        Add(en.Trip, Trip);
        Add(en.Arrival, Arrival);
        Add(en.Departure, Departure);
        Add(en.Duration, Duration);
        Add(en.Destination, Destination);
        Add(en.Transfer, Transfer);
        Add(en.Transport, Transport);
        Add(en.Vehicle, Vehicle);
        Add(en.Route, Route);
        Add(en.PickUp, PickUp);
        Add(en.DropOff, DropOff);
        Add(en.QuoteSummary, QuoteSummary);
        Add(en.QuotedAmount, QuotedAmount);
        Add(en.ItineraryOverview, ItineraryOverview);
        Add(en.WhereYouStay, WhereYouStay);
        Add(en.Inclusions, Inclusions);
        Add(en.Exclusions, Exclusions);
        Add(en.TermsAndConditions, TermsAndConditions);
        Add("Terms &amp; conditions", TermsAndConditions);
        Add(en.Cancellation, Cancellation);
        Add(en.SupplementCosts, SupplementCosts);
        Add(en.SupplementsAndNotes, SupplementsAndNotes);
        Add("Supplements &amp; notes", SupplementsAndNotes);
        Add(en.BankDetails, BankDetails);
        Add(en.PaymentQr, PaymentQr);
        Add(en.PayOnline, PayOnline);
        Add(en.AccountHolder, AccountHolder);
        Add("Account Holder", AccountHolder);
        Add(en.AccountNumber, AccountNumber);
        Add("Account Number", AccountNumber);
        Add(en.AccountNoShort, AccountNoShort);
        Add(en.Bank, Bank);
        Add(en.Ifsc, Ifsc);
        Add(en.AboutUs, AboutUs);
        Add(en.AboutUsLine1, AboutUsLine1);
        Add(en.AboutUsLine2, AboutUsLine2);
        Add(en.AboutUsThanks, AboutUsThanks);
        Add(en.LeadershipAndContact, LeadershipAndContact);
        Add("Leadership &amp; contact", LeadershipAndContact);
        Add(en.ManagingDirector, ManagingDirector);
        Add(en.SalesHead, SalesHead);
        Add(en.ContactLabel, ContactLabel);
        Add(en.RegisteredOffice, RegisteredOffice);
        Add(en.Website, Website);
        Add(en.TravelItinerary, TravelItinerary);
        Add(en.HolidayQuote, HolidayQuote);
        Add(en.DefaultRegisteredOfficeAddressHtml, DefaultRegisteredOfficeAddressHtml);
        Add(
            "3rd Floor, Asad Complex Sangrama, Sopore,<br />\n          Jammu and Kashmir — 193201",
            DefaultRegisteredOfficeAddressHtml);
        Add(
            "3rd Floor, Asad Complex Sangrama, Sopore,<br />Jammu and Kashmir — 193201",
            DefaultRegisteredOfficeAddressHtml);

        return pairs.OrderByDescending(p => p.Item1.Length).ToList();
    }

    public static PdfLocalizedStrings ForLanguage(string? language) =>
        PdfLanguageCodes.Normalize(language) switch
        {
            PdfLanguageCodes.Hindi => Hindi(),
            PdfLanguageCodes.Malayalam => Malayalam(),
            PdfLanguageCodes.Tamil => Tamil(),
            PdfLanguageCodes.Telugu => Telugu(),
            PdfLanguageCodes.Kannada => Kannada(),
            PdfLanguageCodes.Marathi => Marathi(),
            _ => English()
        };

    public static PdfLocalizedStrings English() => new()
    {
        LanguageCode = PdfLanguageCodes.English,
        Culture = CultureInfo.GetCultureInfo("en-IN")
    };

    public static PdfLocalizedStrings Hindi() => new()
    {
        LanguageCode = PdfLanguageCodes.Hindi,
        Culture = CultureInfo.GetCultureInfo("hi-IN"),
        HolidayQuote = "अवकाश कोट",
        TravelItinerary = "यात्रा कार्यक्रम",
        DearClientSalutation = "प्रिय {{ClientName}},",
        ThankYouChoosingTour = "अपनी यात्रा के लिए {{AgencyName}} चुनने के लिए धन्यवाद।",
        GreetingBody =
            "हमें आपकी यात्रा का हिस्सा बनकर खुशी हो रही है और हम आराम, सुंदरता और अविस्मरणीय पलों से भरी यादगार यात्रा अनुभव प्रदान करने की आशा करते हैं।",
        Guest = "अतिथि",
        Phone = "फ़ोन",
        EmailLabel = "ईमेल",
        PaxLabel = "यात्री",
        MealPlan = "भोजन योजना",
        Trip = "यात्रा",
        Arrival = "आगमन",
        Departure = "प्रस्थान",
        Duration = "अवधि",
        Destination = "गंतव्य",
        Transfer = "स्थानांतरण",
        Transport = "परिवहन",
        Vehicle = "वाहन",
        Route = "मार्ग",
        PickUp = "पिक-अप",
        DropOff = "ड्रॉप-ऑफ",
        DefaultDestination = "जम्मू और कश्मीर",
        QuoteSummary = "कोट सारांश",
        QuotedAmount = "कोट राशि",
        ItineraryOverview = "कार्यक्रम अवलोकन",
        WhereYouStay = "आप कहाँ ठहरेंगे",
        Inclusions = "शामिल",
        Exclusions = "शामिल नहीं",
        TermsAndConditions = "नियम और शर्तें",
        Cancellation = "रद्दीकरण",
        SupplementCosts = "अतिरिक्त लागत",
        SupplementsAndNotes = "अनुपूरक और नोट्स",
        BankDetails = "बैंक विवरण",
        PaymentQr = "भुगतान QR",
        PayOnline = "ऑनलाइन भुगतान",
        AccountHolder = "खाताधारक",
        Bank = "बैंक",
        AccountNumber = "खाता संख्या",
        AccountNoShort = "खाता नं.",
        Ifsc = "IFSC",
        AboutUs = "हमारे बारे में",
        AboutUsLine1 =
            "<strong>{{AgencyName}}</strong> विचारशील यात्रा कार्यक्रम, विश्वसनीय सेवा और समृद्ध अनुभव प्रदान करने के लिए प्रतिबद्ध है।",
        AboutUsLine2 =
            "हमारी टीम आगमन से प्रस्थान तक आराम, स्पष्टता और यादगार पलों के लिए अतिथियों के साथ मिलकर काम करती है।",
        AboutUsThanks = "आपके विश्वास के लिए धन्यवाद; हम आपकी यात्रा का हिस्सा बनने की आशा करते हैं।",
        LeadershipAndContact = "नेतृत्व और संपर्क",
        ManagingDirector = "प्रबंध निदेशक",
        SalesHead = "बिक्री प्रमुख",
        ContactLabel = "संपर्क",
        RegisteredOffice = "पंजीकृत कार्यालय",
        Website = "वेबसाइट",
        Hotel = "होटल",
        Houseboat = "हाउसबोट",
        Night = "रात",
        Nights = "रातें",
        Day = "दिन",
        Days = "दिन",
        Rooms = "कमरे",
        ExtraBed = "अतिरिक्त बिस्तर",
        Cnb = "बच्चा (बिस्तर नहीं)",
        Adults = "वयस्क",
        Children = "बच्चे",
        DayActivities = "दिन की गतिविधियाँ",
        HotelName = "होटल का नाम",
        HotelArea = "होटल क्षेत्र",
        DefaultRegisteredOfficeAddressHtml =
            "तीसरी मंज़िल, असाद कॉम्प्लेक्स संग्रामा, सोपोर,<br />जम्मू और कश्मीर — 193201"
    };

    public static PdfLocalizedStrings Malayalam() => new()
    {
        LanguageCode = PdfLanguageCodes.Malayalam,
        Culture = CultureInfo.GetCultureInfo("ml-IN"),
        HolidayQuote = "അവധി ക്വോട്ട്",
        TravelItinerary = "യാത്രാ ക്രമീകരണം",
        DearClientSalutation = "പ്രിയ {{ClientName}},",
        ThankYouChoosingTour = "നിങ്ങളുടെ യാത്രയ്ക്ക് {{AgencyName}} തിരഞ്ഞെടുത്തതിന് നന്ദി.",
        GreetingBody =
            "നിങ്ങളുടെ യാത്രയുടെ ഭാഗമാകാൻ ഞങ്ങൾ സന്തുഷ്ടരാണ്; സുഖം, സൗന്ദര്യം, മറക്കാനാവാത്ത നിമിഷങ്ങൾ എന്നിവയുള്ള ഓർമ്മപ്പെടുത്തുന്ന അനുഭവം നൽകാൻ ഞങ്ങൾ ആഗ്രഹിക്കുന്നു.",
        Guest = "അതിഥി",
        Phone = "ഫോൺ",
        EmailLabel = "ഇമെയിൽ",
        PaxLabel = "യാത്രക്കാർ",
        MealPlan = "ഭക്ഷണ പദ്ധതി",
        Trip = "യാത്ര",
        Arrival = "എത്തൽ",
        Departure = "പുറപ്പെടൽ",
        Duration = "കാലാവധി",
        Destination = "ലക്ഷ്യസ്ഥാനം",
        Transfer = "ട്രാൻസ്ഫർ",
        Transport = "ഗതാഗതം",
        Vehicle = "വാഹനം",
        Route = "മാർഗം",
        PickUp = "പിക്ക്-അപ്പ്",
        DropOff = "ഡ്രോപ്പ്-ഓഫ്",
        DefaultDestination = "ജമ്മു കശ്മീർ",
        QuoteSummary = "ക്വോട്ട് സംഗ്രഹം",
        QuotedAmount = "ക്വോട്ട് തുക",
        ItineraryOverview = "ഇടനാഴി അവലോകനം",
        WhereYouStay = "നിങ്ങൾ താമസിക്കുന്നിടം",
        Inclusions = "ഉൾപ്പെടുന്നവ",
        Exclusions = "ഉൾപ്പെടാത്തവ",
        TermsAndConditions = "നിബന്ധനകളും വ്യവസ്ഥകളും",
        Cancellation = "റദ്ദാക്കൽ",
        SupplementCosts = "അധിക ചെലവുകൾ",
        SupplementsAndNotes = "അനുബന്ധങ്ങളും കുറിപ്പുകളും",
        BankDetails = "ബാങ്ക് വിവരങ്ങൾ",
        PaymentQr = "പേയ്‌മെന്റ് QR",
        PayOnline = "ഓൺലൈൻ പേയ്‌മെന്റ്",
        AccountHolder = "അക്കൗണ്ട് ഉടമ",
        Bank = "ബാങ്ക്",
        AccountNumber = "അക്കൗണ്ട് നമ്പർ",
        AccountNoShort = "അക്കൗണ്ട് നം.",
        Ifsc = "IFSC",
        AboutUs = "ഞങ്ങളെക്കുറിച്ച്",
        AboutUsLine1 =
            "<strong>{{AgencyName}}</strong> ചിന്താപരമായ ഇടനാഴികൾ, വിശ്വസനീയ സേവനം, സമൃദ്ധമായ യാത്രാ അനുഭവങ്ങൾ എന്നിവ നൽകാൻ പ്രതിജ്ഞാബദ്ധമാണ്.",
        AboutUsLine2 =
            "എത്തുന്ന മുതൽ പുറപ്പെടുന്ന വരെ സുഖം, വ്യക്തത, ഓർമ്മപ്പെടുത്തുന്ന നിമിഷങ്ങൾ എന്നിവ ഉറപ്പാക്കാൻ ഞങ്ങളുടെ ടീം അതിഥികളുമായി അടുത്ത് പ്രവർത്തിക്കുന്നു.",
        AboutUsThanks = "നിങ്ങളുടെ വിശ്വാസത്തിന് നന്ദി; നിങ്ങളുടെ യാത്രയുടെ ഭാഗമാകാൻ ഞങ്ങൾ ആഗ്രഹിക്കുന്നു.",
        LeadershipAndContact = "നേതൃത്വവും ബന്ധപ്പെടുക",
        ManagingDirector = "മാനേജിംഗ് ഡയറക്ടർ",
        SalesHead = "സെയിൽസ് ഹെഡ്",
        ContactLabel = "ബന്ധപ്പെടുക",
        RegisteredOffice = "രജിസ്റ്റർ ചെയ്ത ഓഫീസ്",
        Website = "വെബ്സൈറ്റ്",
        Hotel = "ഹോട്ടൽ",
        Houseboat = "ഹൗസ്ബോട്ട്",
        Night = "രാത്രി",
        Nights = "രാത്രികൾ",
        Day = "ദിവസം",
        Days = "ദിവസങ്ങൾ",
        Rooms = "മുറികൾ",
        ExtraBed = "അധിക കിടക്ക",
        Cnb = "കുട്ടി (കിടക്ക ഇല്ല)",
        Adults = "മുതിർന്നവർ",
        Children = "കുട്ടികൾ",
        DayActivities = "ദിവസത്തെ പ്രവർത്തനങ്ങൾ",
        HotelName = "ഹോട്ടൽ പേര്",
        HotelArea = "ഹോട്ടൽ പ്രദേശം",
        DefaultRegisteredOfficeAddressHtml =
            "3-ാം നില, ആസാദ് കോംപ്ലക്സ് സംഗ്രാമ, സോപ്പൂർ,<br />ജമ്മു കശ്മീർ — 193201"
    };

    public static PdfLocalizedStrings Tamil() => new()
    {
        LanguageCode = PdfLanguageCodes.Tamil,
        Culture = CultureInfo.GetCultureInfo("ta-IN"),
        HolidayQuote = "விடுமுறை மதிப்பீடு",
        TravelItinerary = "பயணத் திட்டம்",
        DearClientSalutation = "அன்புள்ள {{ClientName}},",
        ThankYouChoosingTour = "உங்கள் பயணத்திற்கு {{AgencyName}}-ஐ தேர்ந்தெடுத்ததற்கு நன்றி.",
        GreetingBody =
            "உங்கள் பயணத்தின் ஒரு பகுதியாக இருப்பதில் மகிழ்ச்சி; வசதி, அழகு, மறக்க முடியாத தருணங்கள் நிறைந்த நினைவுபடுத்தும் அனுபவத்தை வழங்க எதிர்பார்க்கிறோம்.",
        Guest = "விருந்தினர்",
        Phone = "தொலைபேசி",
        EmailLabel = "மின்னஞ்சல்",
        PaxLabel = "பயணிகள்",
        MealPlan = "உணவுத் திட்டம்",
        Trip = "பயணம்",
        Arrival = "வருகை",
        Departure = "புறப்படுதல்",
        Duration = "கால அளவு",
        Destination = "இலக்கு",
        Transfer = "போக்குவரத்து",
        Transport = "போக்குவரத்து",
        Vehicle = "வாகனம்",
        Route = "பாதை",
        PickUp = "பிக்-அப்",
        DropOff = "டிராப்-ஆஃப்",
        DefaultDestination = "ஜம்மு காஷ்மீர்",
        QuoteSummary = "மதிப்பீட்டு சுருக்கம்",
        QuotedAmount = "மதிப்பிடப்பட்ட தொகை",
        ItineraryOverview = "பயணத் திட்ட மேலோட்டம்",
        WhereYouStay = "நீங்கள் தங்கும் இடம்",
        Inclusions = "சேர்க்கைகள்",
        Exclusions = "விலக்குகள்",
        TermsAndConditions = "விதிமுறைகள் மற்றும் நிபந்தனைகள்",
        Cancellation = "ரத்து",
        SupplementCosts = "கூடுதல் செலவுகள்",
        SupplementsAndNotes = "துணைக்கட்டணங்கள் & குறிப்புகள்",
        BankDetails = "வங்கி விவரங்கள்",
        PaymentQr = "கட்டண QR",
        PayOnline = "ஆன்லைன் கட்டணம்",
        AccountHolder = "கணக்கு உரிமையாளர்",
        Bank = "வங்கி",
        AccountNumber = "கணக்கு எண்",
        AccountNoShort = "கணக்கு எண்",
        Ifsc = "IFSC",
        AboutUs = "எங்களைப் பற்றி",
        AboutUsLine1 =
            "<strong>{{AgencyName}}</strong> சிந்தனையுடன் கூடிய பயணத் திட்டங்கள், நம்பகமான சேவை, வளமான அனுபவங்களை வழங்க உறுதிபூண்டுள்ளது.",
        AboutUsLine2 =
            "வருகை முதல் புறப்படுதல் வரை வசதி, தெளிவு, நினைவுபடுத்தும் தருணங்களை உறுதிப்படுத்த விருந்தினர்களுடன் எங்கள் குழு நெருக்கமாக பணியாற்றுகிறது.",
        AboutUsThanks = "உங்கள் நம்பிக்கைக்கு நன்றி; உங்கள் பயணத்தின் ஒரு பகுதியாக இருக்க ஆவலுடன் உள்ளோம்.",
        LeadershipAndContact = "தலைமை & தொடர்பு",
        ManagingDirector = "நிர்வாக இயக்குனர்",
        SalesHead = "விற்பனைத் தலைவர்",
        ContactLabel = "தொடர்பு",
        RegisteredOffice = "பதிவு அலுவலகம்",
        Website = "வலைத்தளம்",
        Hotel = "ஹோட்டல்",
        Houseboat = "ஹவுஸ்போட்",
        Night = "இரவு",
        Nights = "இரவுகள்",
        Day = "நாள்",
        Days = "நாட்கள்",
        Rooms = "அறைகள்",
        ExtraBed = "கூடுதல் படுக்கை",
        Cnb = "குழந்தை (படுக்கை இல்லை)",
        Adults = "வயது வந்தோர்",
        Children = "குழந்தைகள்",
        DayActivities = "நாள் செயல்பாடுகள்",
        HotelName = "ஹோட்டல் பெயர்",
        HotelArea = "ஹோட்டல் பகுதி",
        DefaultRegisteredOfficeAddressHtml =
            "3வது மாடி, ஆசாத் காம்ப்ளக்ஸ் சங்கிராமா, சோபூர்,<br />ஜம்மு காஷ்மீர் — 193201"
    };

    public static PdfLocalizedStrings Telugu() => new()
    {
        LanguageCode = PdfLanguageCodes.Telugu,
        Culture = CultureInfo.GetCultureInfo("te-IN"),
        HolidayQuote = "సెలవు కోట్",
        TravelItinerary = "ప్రయాణ ఇటినరరీ",
        DearClientSalutation = "ప్రియమైన {{ClientName}},",
        ThankYouChoosingTour = "మీ టూర్ కోసం {{AgencyName}} ఎంచుకున్నందుకు ధన్యవాదాలు.",
        GreetingBody =
            "మీ ప్రయాణంలో భాగమవ్వడం మాకు సంతోషం; సౌకర్యం, అందం, మరిచిపోలేని క్షణాలతో కూడిన గుర్తుండే అనుభవాన్ని అందించడానికి ఎదురుచూస్తున్నాము.",
        Guest = "అతిథి",
        Phone = "ఫోన్",
        EmailLabel = "ఇమెయిల్",
        PaxLabel = "ప్రయాణికులు",
        MealPlan = "భోజన ప్రణాళిక",
        Trip = "ప్రయాణం",
        Arrival = "రాక",
        Departure = "ప్రయాణం బయలుదేరు",
        Duration = "వ్యవధి",
        Destination = "గమ్యం",
        Transfer = "ట్రాన్స్‌ఫర్",
        Transport = "రవాణా",
        Vehicle = "వాహనం",
        Route = "మార్గం",
        PickUp = "పిక్-అప్",
        DropOff = "డ్రాప్-ఆఫ్",
        DefaultDestination = "జమ్మూ కాశ్మీర్",
        QuoteSummary = "కోట్ సారాంశం",
        QuotedAmount = "కోట్ మొత్తం",
        ItineraryOverview = "ఇటినరరీ అవలోకనం",
        WhereYouStay = "మీరు ఎక్కడ బస చేస్తారు",
        Inclusions = "చేర్పులు",
        Exclusions = "మినహాయింపులు",
        TermsAndConditions = "నిబంధనలు & షరతులు",
        Cancellation = "రద్దు",
        SupplementCosts = "అదనపు ఖర్చులు",
        SupplementsAndNotes = "అనుబంధాలు & గమనికలు",
        BankDetails = "బ్యాంక్ వివరాలు",
        PaymentQr = "చెల్లింపు QR",
        PayOnline = "ఆన్‌లైన్ చెల్లింపు",
        AccountHolder = "ఖాతాదారు",
        Bank = "బ్యాంక్",
        AccountNumber = "ఖాతా సంఖ్య",
        AccountNoShort = "ఖాతా నం.",
        Ifsc = "IFSC",
        AboutUs = "మా గురించి",
        AboutUsLine1 =
            "<strong>{{AgencyName}}</strong> ఆలోచనాపూర్వక ఇటినరరీలు, నమ్మకమైన సేవ, సమృద్ధ అనుభవాలను అందించడానికి కట్టుబడి ఉంది.",
        AboutUsLine2 =
            "రాక నుండి బయలుదేరు వరకు సౌకర్యం, స్పష్టత, గుర్తుండే క్షణాలను నిర్ధారించడానికి మా బృందం అతిథులతో దగ్గరగా పని చేస్తుంది.",
        AboutUsThanks = "మీ విశ్వాసానికి ధన్యవాదాలు; మీ ప్రయాణంలో భాగమవ్వడానికి ఎదురుచూస్తున్నాము.",
        LeadershipAndContact = "నాయకత్వం & సంప్రదింపు",
        ManagingDirector = "మేనేజింగ్ డైరెక్టర్",
        SalesHead = "సేల్స్ హెడ్",
        ContactLabel = "సంప్రదింపు",
        RegisteredOffice = "నమోదిత కార్యాలయం",
        Website = "వెబ్‌సైట్",
        Hotel = "హోటల్",
        Houseboat = "హౌస్‌బోట్",
        Night = "రాత్రి",
        Nights = "రాత్రులు",
        Day = "రోజు",
        Days = "రోజులు",
        Rooms = "గదులు",
        ExtraBed = "అదనపు మంచం",
        Cnb = "పిల్ల (మంచం లేదు)",
        Adults = "పెద్దలు",
        Children = "పిల్లలు",
        DayActivities = "రోజు కార్యకలాపాలు",
        HotelName = "హోటల్ పేరు",
        HotelArea = "హోటల్ ప్రాంతం",
        DefaultRegisteredOfficeAddressHtml =
            "3వ అంతస్తు, ఆసాద్ కాంప్లెక్స్ సంగ్రామ, సోపూర్,<br />జమ్మూ కాశ్మీర్ — 193201"
    };

    public static PdfLocalizedStrings Kannada() => new()
    {
        LanguageCode = PdfLanguageCodes.Kannada,
        Culture = CultureInfo.GetCultureInfo("kn-IN"),
        HolidayQuote = "ರಜೆ ಉಲ್ಲೇಖ",
        TravelItinerary = "ಪ್ರಯಾಣ ಕಾರ್ಯಕ್ರಮ",
        DearClientSalutation = "ಪ್ರಿಯ {{ClientName}},",
        ThankYouChoosingTour = "ನಿಮ್ಮ ಪ್ರವಾಸಕ್ಕಾಗಿ {{AgencyName}} ಆಯ್ಕೆ ಮಾಡಿದ್ದಕ್ಕಾಗಿ ಧನ್ಯವಾದಗಳು.",
        GreetingBody =
            "ನಿಮ್ಮ ಪ್ರಯಾಣದ ಭಾಗವಾಗಲು ನಾವು ಸಂತೋಷಿಸುತ್ತೇವೆ; ಆರಾಮ, ಸೌಂದರ್ಯ ಮತ್ತು ಮರೆಯಲಾಗದ ಕ್ಷಣಗಳೊಂದಿಗೆ ಸ್ಮರಣೀಯ ಅನುಭವವನ್ನು ನೀಡಲು ಎದುರು ನೋಡುತ್ತಿದ್ದೇವೆ.",
        Guest = "ಅತಿಥಿ",
        Phone = "ದೂರವಾಣಿ",
        EmailLabel = "ಇಮೇಲ್",
        PaxLabel = "ಪ್ರಯಾಣಿಕರು",
        MealPlan = "ಊಟ ಯೋಜನೆ",
        Trip = "ಪ್ರಯಾಣ",
        Arrival = "ಬರುವಿಕೆ",
        Departure = "ಹೊರಡುವಿಕೆ",
        Duration = "ಅವಧಿ",
        Destination = "ಗಮ್ಯಸ್ಥಾನ",
        Transfer = "ವರ್ಗಾವಣೆ",
        Transport = "ಸಾರಿಗೆ",
        Vehicle = "ವಾಹನ",
        Route = "ಮಾರ್ಗ",
        PickUp = "ಪಿಕ್-ಅಪ್",
        DropOff = "ಡ್ರಾಪ್-ಆಫ್",
        DefaultDestination = "ಜಮ್ಮು ಕಾಶ್ಮೀರ",
        QuoteSummary = "ಉಲ್ಲೇಖ ಸಾರಾಂಶ",
        QuotedAmount = "ಉಲ್ಲೇಖ ಮೊತ್ತ",
        ItineraryOverview = "ಕಾರ್ಯಕ್ರಮ ಅವಲೋಕನ",
        WhereYouStay = "ನೀವು ಎಲ್ಲಿ ತಂಗುತ್ತೀರಿ",
        Inclusions = "ಒಳಗೊಂಡಿದೆ",
        Exclusions = "ಒಳಗೊಂಡಿಲ್ಲ",
        TermsAndConditions = "ನಿಯಮಗಳು ಮತ್ತು ಷರತ್ತುಗಳು",
        Cancellation = "ರದ್ದತಿ",
        SupplementCosts = "ಹೆಚ್ಚುವರಿ ವೆಚ್ಚಗಳು",
        SupplementsAndNotes = "ಪೂರಕಗಳು & ಟಿಪ್ಪಣಿಗಳು",
        BankDetails = "ಬ್ಯಾಂಕ್ ವಿವರಗಳು",
        PaymentQr = "ಪಾವತಿ QR",
        PayOnline = "ಆನ್‌ಲೈನ್ ಪಾವತಿ",
        AccountHolder = "ಖಾತೆದಾರ",
        Bank = "ಬ್ಯಾಂಕ್",
        AccountNumber = "ಖಾತೆ ಸಂಖ್ಯೆ",
        AccountNoShort = "ಖಾತೆ ಸಂ.",
        Ifsc = "IFSC",
        AboutUs = "ನಮ್ಮ ಬಗ್ಗೆ",
        AboutUsLine1 =
            "<strong>{{AgencyName}}</strong> ಚಿಂತನಶೀಲ ಕಾರ್ಯಕ್ರಮಗಳು, ವಿಶ್ವಾಸಾರ್ಹ ಸೇವೆ ಮತ್ತು ಸಮೃದ್ಧ ಅನುಭವಗಳನ್ನು ನೀಡಲು ಬದ್ಧವಾಗಿದೆ.",
        AboutUsLine2 =
            "ಬರುವಿಕೆಯಿಂದ ಪ್ರಸ್ಥಾನದವರೆಗೆ ಆರಾಮ, ಸ್ಪಷ್ಟತೆ ಮತ್ತು ಸ್ಮರಣೀಯ ಕ್ಷಣಗಳನ್ನು ಖಚಿತಪಡಿಸಲು ನಮ್ಮ ತಂಡ ಅತಿಥಿಗಳೊಂದಿಗೆ ಸಮೀಪದಿಂದ ಕೆಲಸ ಮಾಡುತ್ತದೆ.",
        AboutUsThanks = "ನಿಮ್ಮ ನಂಬಿಕೆಗೆ ಧನ್ಯವಾದಗಳು; ನಿಮ್ಮ ಪ್ರಯಾಣದ ಭಾಗವಾಗಲು ಎದುರು ನೋಡುತ್ತಿದ್ದೇವೆ.",
        LeadershipAndContact = "ನಾಯಕತ್ವ ಮತ್ತು ಸಂಪರ್ಕ",
        ManagingDirector = "ವ್ಯವಸ್ಥಾಪಕ ನಿರ್ದೇಶಕ",
        SalesHead = "ಮಾರಾಟ ಮುಖ್ಯಸ್ಥ",
        ContactLabel = "ಸಂಪರ್ಕ",
        RegisteredOffice = "ನೋಂದಾಯಿತ ಕಚೇರಿ",
        Website = "ಜಾಲತಾಣ",
        Hotel = "ಹೋಟೆಲ್",
        Houseboat = "ಹೌಸ್‌ಬೋಟ್",
        Night = "ರಾತ್ರಿ",
        Nights = "ರಾತ್ರಿಗಳು",
        Day = "ದಿನ",
        Days = "ದಿನಗಳು",
        Rooms = "ಕೊಠಡಿಗಳು",
        ExtraBed = "ಹೆಚ್ಚುವರಿ ಹಾಸಿಗೆ",
        Cnb = "ಮಗು (ಹಾಸಿಗೆ ಇಲ್ಲ)",
        Adults = "ವಯಸ್ಕರು",
        Children = "ಮಕ್ಕಳು",
        DayActivities = "ದಿನದ ಚಟುವಟಿಕೆಗಳು",
        HotelName = "ಹೋಟೆಲ್ ಹೆಸರು",
        HotelArea = "ಹೋಟೆಲ್ ಪ್ರದೇಶ",
        DefaultRegisteredOfficeAddressHtml =
            "3ನೇ ಮಹಡಿ, ಆಸಾದ್ ಕಾಂಪ್ಲೆಕ್ಸ್ ಸಂಗ್ರಾಮ, ಸೋಪೋರ್,<br />ಜಮ್ಮು ಕಾಶ್ಮೀರ್ — 193201"
    };

    public static PdfLocalizedStrings Marathi() => new()
    {
        LanguageCode = PdfLanguageCodes.Marathi,
        Culture = CultureInfo.GetCultureInfo("mr-IN"),
        HolidayQuote = "सुट्टी कोट",
        TravelItinerary = "प्रवास कार्यक्रम",
        DearClientSalutation = "प्रिय {{ClientName}},",
        ThankYouChoosingTour = "आपल्या सहलीसाठी {{AgencyName}} निवडल्याबद्दल धन्यवाद.",
        GreetingBody =
            "आपल्या प्रवासाचा भाग व्हायला आम्हाला आनंद आहे; आराम, सौंदर्य आणि विसरण्याजोग्या क्षणांनी भरलेला संस्मरणीय अनुभव देण्याची आम्ही अपेक्षा करतो.",
        Guest = "पाहुणे",
        Phone = "फोन",
        EmailLabel = "ईमेल",
        PaxLabel = "प्रवासी",
        MealPlan = "जेवण योजना",
        Trip = "सहल",
        Arrival = "आगमन",
        Departure = "प्रस्थान",
        Duration = "कालावधी",
        Destination = "गंतव्य",
        Transfer = "वाहतूक",
        Transport = "वाहतूक",
        Vehicle = "वाहन",
        Route = "मार्ग",
        PickUp = "पिक-अप",
        DropOff = "ड्रॉप-ऑफ",
        DefaultDestination = "जम्मू आणि काश्मीर",
        QuoteSummary = "कोट सारांश",
        QuotedAmount = "कोट रक्कम",
        ItineraryOverview = "कार्यक्रम आढावा",
        WhereYouStay = "आपण कुठे राहाल",
        Inclusions = "समाविष्ट",
        Exclusions = "वगळलेले",
        TermsAndConditions = "अटी व शर्ती",
        Cancellation = "रद्दीकरण",
        SupplementCosts = "अतिरिक्त खर्च",
        SupplementsAndNotes = "पूरक आणि टिपा",
        BankDetails = "बँक तपशील",
        PaymentQr = "पेमेंट QR",
        PayOnline = "ऑनलाइन पेमेंट",
        AccountHolder = "खातेदार",
        Bank = "बँक",
        AccountNumber = "खाते क्रमांक",
        AccountNoShort = "खाते क्र.",
        Ifsc = "IFSC",
        AboutUs = "आमच्याबद्दल",
        AboutUsLine1 =
            "<strong>{{AgencyName}}</strong> विचारपूर्ण कार्यक्रम, विश्वासार्ह सेवा आणि समृद्ध अनुभव देण्यासाठी वचनबद्ध आहे.",
        AboutUsLine2 =
            "आगमनापासून प्रस्थानापर्यंत आराम, स्पष्टता आणि संस्मरणीय क्षण सुनिश्चित करण्यासाठी आमची टीम पाहुण्यांसोबत जवळून काम करते.",
        AboutUsThanks = "आपल्या विश्वासाबद्दल धन्यवाद; आपल्या प्रवासाचा भाग व्हायला आम्ही आतुर आहोत.",
        LeadershipAndContact = "नेतृत्व आणि संपर्क",
        ManagingDirector = "व्यवस्थापकीय संचालक",
        SalesHead = "विक्री प्रमुख",
        ContactLabel = "संपर्क",
        RegisteredOffice = "नोंदणीकृत कार्यालय",
        Website = "वेबसाइट",
        Hotel = "हॉटेल",
        Houseboat = "हाउसबोट",
        Night = "रात्र",
        Nights = "रात्री",
        Day = "दिवस",
        Days = "दिवस",
        Rooms = "खोल्या",
        ExtraBed = "अतिरिक्त बेड",
        Cnb = "मूल (बेड नाही)",
        Adults = "प्रौढ",
        Children = "मुले",
        DayActivities = "दिवसाच्या क्रियाकलाप",
        HotelName = "हॉटेल नाव",
        HotelArea = "हॉटेल क्षेत्र",
        DefaultRegisteredOfficeAddressHtml =
            "तिसरा मजला, आसाद कॉम्प्लेक्स संग्रामा, सोपोर,<br />जम्मू आणि काश्मीर — 193201"
    };
}
