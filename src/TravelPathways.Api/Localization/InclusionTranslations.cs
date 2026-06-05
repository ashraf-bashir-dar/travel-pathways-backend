namespace TravelPathways.Api.Localization;

/// <summary>Localized inclusion/exclusion line items for package PDFs.</summary>
public static class InclusionTranslations
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ByLanguage =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            [PdfLanguageCodes.English] = English(),
            [PdfLanguageCodes.Hindi] = Hindi(),
            [PdfLanguageCodes.Malayalam] = Malayalam(),
            [PdfLanguageCodes.Tamil] = Tamil(),
            [PdfLanguageCodes.Telugu] = Telugu(),
            [PdfLanguageCodes.Kannada] = Kannada(),
            [PdfLanguageCodes.Marathi] = Marathi()
        };

    public static string? GetLabel(string inclusionId, string? language)
    {
        var lang = PdfLanguageCodes.Normalize(language);
        if (!ByLanguage.TryGetValue(lang, out var map)) return null;
        return map.TryGetValue(inclusionId, out var label) ? label : null;
    }

    private static Dictionary<string, string> English() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["welcome_greeting"] = "Welcome and greeting",
        ["sightseeing_as_per_itinerary"] = "Sightseeing as per itinerary",
        ["shikara_1hr"] = "1-hour Shikara ride (complimentary)",
        ["accommodation_mentioned_hotels"] = "Accommodation in above mentioned hotels",
        ["transportation"] = "Transportation",
        ["meals_dinner_breakfast"] = "Meals (Dinner and Breakfast)",
        ["gondola_phase1"] = "Gondola tickets (Phase 1)",
        ["gondola_phase2"] = "Gondola tickets (Phase 2)",
        ["mugal_gardens"] = "Entry tickets for Srinagar Mughal Gardens",
        ["extended_stay"] = "Extended stay or travel due to any reason",
        ["meals_not_specified"] = "Any meals not specified in the tour cost",
        ["union_cabs_pony"] = "Local cabs in Gulmarg, Sonmarg, Pahalgam and pony rides"
    };

    private static Dictionary<string, string> Hindi() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["welcome_greeting"] = "स्वागत और अभिवादन",
        ["sightseeing_as_per_itinerary"] = "कार्यक्रम के अनुसार दर्शनीय स्थल",
        ["shikara_1hr"] = "1 घंटे की शिकारा सवारी (निःशुल्क)",
        ["accommodation_mentioned_hotels"] = "उल्लिखित होटलों में आवास",
        ["transportation"] = "परिवहन",
        ["meals_dinner_breakfast"] = "भोजन (रात का खाना और नाश्ता)",
        ["gondola_phase1"] = "गोंडोला टिकट (चरण 1)",
        ["gondola_phase2"] = "गोंडोला टिकट (चरण 2)",
        ["mugal_gardens"] = "श्रीनगर मुगल गार्डन प्रवेश टिकट",
        ["extended_stay"] = "किसी भी कारण से विस्तारित प्रवास या यात्रा",
        ["meals_not_specified"] = "टूर लागत में निर्दिष्ट नहीं कोई भी भोजन",
        ["union_cabs_pony"] = "गुलमर्ग, सोनमर्ग, पहलगाम में स्थानीय कैब और पोनी सवारी"
    };

    private static Dictionary<string, string> Malayalam() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["welcome_greeting"] = "സ്വാഗതവും അഭിവാദ്യവും",
        ["sightseeing_as_per_itinerary"] = "ഇടനാഴി പ്രകാരമുള്ള സൈറ്റ് സീയിംഗ്",
        ["shikara_1hr"] = "1 മണിക്കൂർ ഷിക്കാര യാത്ര (സൗജന്യം)",
        ["accommodation_mentioned_hotels"] = "പരാമർശിച്ച ഹോട്ടലുകളിൽ താമസം",
        ["transportation"] = "ഗതാഗതം",
        ["meals_dinner_breakfast"] = "ഭക്ഷണം (രാത്രി ഭക്ഷണവും പ്രഭാതഭക്ഷണവും)",
        ["gondola_phase1"] = "ഗോണ്ടോള ടിക്കറ്റ് (ഘട്ടം 1)",
        ["gondola_phase2"] = "ഗോണ്ടോള ടിക്കറ്റ് (ഘട്ടം 2)",
        ["mugal_gardens"] = "ശ്രീനഗർ മുഗൾ ഗാർഡൻ പ്രവേശന ടിക്കറ്റ്",
        ["extended_stay"] = "ഏതെങ്കിലും കാരണത്താൽ ദീർഘിച്ച താമസം അല്ലെങ്കിൽ യാത്ര",
        ["meals_not_specified"] = "ടൂർ ചെലവിൽ വ്യക്തമാക്കാത്ത ഭക്ഷണം",
        ["union_cabs_pony"] = "ഗുൽമാർഗ്, സോൺമാർഗ്, പഹൽഗാമിലെ പ്രാദേശിക ക്യാബുകളും പോണി സവാരിയും"
    };

    private static Dictionary<string, string> Tamil() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["welcome_greeting"] = "வரவேற்பு மற்றும் வாழ்த்து",
        ["sightseeing_as_per_itinerary"] = "பயணத் திட்டப்படி சுற்றுலா",
        ["shikara_1hr"] = "1 மணி நேர ஷிகாரா பயணம் (இலவசம்)",
        ["accommodation_mentioned_hotels"] = "குறிப்பிட்ட ஹோட்டல்களில் தங்குமிடம்",
        ["transportation"] = "போக்குவரத்து",
        ["meals_dinner_breakfast"] = "உணவு (இரவு உணவு மற்றும் காலை உணவு)",
        ["gondola_phase1"] = "கொண்டோலா டிக்கெட் (கட்டம் 1)",
        ["gondola_phase2"] = "கொண்டோலா டிக்கெட் (கட்டம் 2)",
        ["mugal_gardens"] = "ஸ்ரீநகர் முகல் தோட்டங்கள் நுழைவு டிக்கெட்",
        ["extended_stay"] = "எந்த காரணத்திற்காகவும் நீட்டிக்கப்பட்ட தங்குதல் அல்லது பயணம்",
        ["meals_not_specified"] = "சுற்றுலா செலவில் குறிப்பிடப்படாத உணவு",
        ["union_cabs_pony"] = "குல்மார்க், சோன்மார்க், பஹல்காமில் உள்ளூர் கேப் மற்றும் குதிரை சவாரி"
    };

    private static Dictionary<string, string> Telugu() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["welcome_greeting"] = "స్వాగతం మరియు అభినందన",
        ["sightseeing_as_per_itinerary"] = "ఇటినరరీ ప్రకారం సైట్ సీయింగ్",
        ["shikara_1hr"] = "1 గంట షికారా రైడ్ (ఉచితం)",
        ["accommodation_mentioned_hotels"] = "పైన పేర్కొన్న హోటళ్లలో వసతి",
        ["transportation"] = "రవాణా",
        ["meals_dinner_breakfast"] = "భోజనం (రాత్రి భోజనం మరియు అల్పాహారం)",
        ["gondola_phase1"] = "గొండోలా టికెట్ (దశ 1)",
        ["gondola_phase2"] = "గొండోలా టికెట్ (దశ 2)",
        ["mugal_gardens"] = "శ్రీనగర్ ముగల్ తోటల ప్రవేశ టికెట్",
        ["extended_stay"] = "ఏదైనా కారణంగా పొడిగించిన బస లేదా ప్రయాణం",
        ["meals_not_specified"] = "టూర్ ఖర్చులో పేర్కొనని భోజనం",
        ["union_cabs_pony"] = "గుల్మార్గ్, సోన్మార్గ్, పహల్గామ్‌లో స్థానిక క్యాబ్‌లు మరియు పోనీ రైడ్‌లు"
    };

    private static Dictionary<string, string> Kannada() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["welcome_greeting"] = "ಸ್ವಾಗತ ಮತ್ತು ಅಭಿನಂದನೆ",
        ["sightseeing_as_per_itinerary"] = "ಕಾರ್ಯಕ್ರಮದ ಪ್ರಕಾರ ಸೈಟ್ ಸೀಯಿಂಗ್",
        ["shikara_1hr"] = "1 ಗಂಟೆ ಶಿಕಾರಾ ಸವಾರಿ (ಉಚಿತ)",
        ["accommodation_mentioned_hotels"] = "ಹೆಚ್ಚಿಸಲಾದ ಹೋಟೆಲ್‌ಗಳಲ್ಲಿ ವಸತಿ",
        ["transportation"] = "ಸಾರಿಗೆ",
        ["meals_dinner_breakfast"] = "ಊಟ (ರಾತ್ರಿ ಊಟ ಮತ್ತು ಉಪಾಹಾರ)",
        ["gondola_phase1"] = "ಗೊಂಡೋಲಾ ಟಿಕೆಟ್ (ಹಂತ 1)",
        ["gondola_phase2"] = "ಗೊಂಡೋಲಾ ಟಿಕೆಟ್ (ಹಂತ 2)",
        ["mugal_gardens"] = "ಶ್ರೀನಗರ ಮುಗಲ್ ಉದ್ಯಾನ ಪ್ರವೇಶ ಟಿಕೆಟ್",
        ["extended_stay"] = "ಯಾವುದೇ ಕಾರಣಕ್ಕೆ ವಿಸ್ತರಿಸಿದ ತಂಗುವಿಕೆ ಅಥವಾ ಪ್ರಯಾಣ",
        ["meals_not_specified"] = "ಪ್ರವಾಸ ವೆಚ್ಚದಲ್ಲಿ ನಿರ್ದಿಷ್ಟಪಡಿಸದ ಊಟ",
        ["union_cabs_pony"] = "ಗುಲ್ಮಾರ್ಗ್, ಸೋನ್ಮಾರ್ಗ್, ಪಹಲ್ಗಾಮ್‌ನಲ್ಲಿ ಸ್ಥಳೀಯ ಕ್ಯಾಬ್‌ಗಳು ಮತ್ತು ಪೋನಿ ಸವಾರಿ"
    };

    private static Dictionary<string, string> Marathi() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["welcome_greeting"] = "स्वागत आणि अभिवादन",
        ["sightseeing_as_per_itinerary"] = "कार्यक्रमानुसार पर्यटन",
        ["shikara_1hr"] = "1 तास शिकारा सवारी (विनामूल्य)",
        ["accommodation_mentioned_hotels"] = "वर नमूद केलेल्या हॉटेलमध्ये राहण्याची सोय",
        ["transportation"] = "वाहतूक",
        ["meals_dinner_breakfast"] = "जेवण (रात्रीचे जेवण आणि नाश्ता)",
        ["gondola_phase1"] = "गोंडोला तिकीट (टप्पा 1)",
        ["gondola_phase2"] = "गोंडोला तिकीट (टप्पा 2)",
        ["mugal_gardens"] = "श्रीनगर मुघल गार्डन प्रवेश तिकीट",
        ["extended_stay"] = "कोणत्याही कारणास्तव विस्तारित मुक्काम किंवा प्रवास",
        ["meals_not_specified"] = "टूर खर्चात नमूद न केलेले कोणतेही जेवण",
        ["union_cabs_pony"] = "गुलमर्ग, सोनमार्ग, पहलगाममध्ये स्थानिक कॅब आणि पोनी सवारी"
    };
}
