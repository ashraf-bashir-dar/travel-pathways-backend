using TravelPathways.Api.Common;

namespace TravelPathways.Api.Localization;

public static class MealPlanTranslations
{
    public static string Label(AccommodationMealPlan? plan, string? language)
    {
        if (!plan.HasValue) return "–";
        var lang = PdfLanguageCodes.Normalize(language);
        if (lang == PdfLanguageCodes.English)
            return English(plan.Value);

        return lang switch
        {
            PdfLanguageCodes.Hindi => Hindi(plan.Value),
            PdfLanguageCodes.Malayalam => Malayalam(plan.Value),
            PdfLanguageCodes.Tamil => Tamil(plan.Value),
            PdfLanguageCodes.Telugu => Telugu(plan.Value),
            PdfLanguageCodes.Kannada => Kannada(plan.Value),
            PdfLanguageCodes.Marathi => Marathi(plan.Value),
            _ => English(plan.Value)
        };
    }

    private static string English(AccommodationMealPlan plan) => plan switch
    {
        AccommodationMealPlan.AP => "Breakfast + Lunch + Dinner",
        AccommodationMealPlan.MAP => "MAP (Dinner + Breakfast)",
        AccommodationMealPlan.CP => "CP",
        AccommodationMealPlan.BreakfastOnly => "Breakfast Only",
        AccommodationMealPlan.RoomOnly => "EP (Room only)",
        _ => plan.ToString()
    };

    private static string Hindi(AccommodationMealPlan plan) => plan switch
    {
        AccommodationMealPlan.AP => "नाश्ता + दोपहर का भोजन + रात का खाना",
        AccommodationMealPlan.MAP => "MAP (रात का खाना + नाश्ता)",
        AccommodationMealPlan.CP => "CP",
        AccommodationMealPlan.BreakfastOnly => "केवल नाश्ता",
        AccommodationMealPlan.RoomOnly => "EP (केवल कमरा)",
        _ => plan.ToString()
    };

    private static string Malayalam(AccommodationMealPlan plan) => plan switch
    {
        AccommodationMealPlan.AP => "പ്രഭാതം + ഉച്ചഭക്ഷണം + രാത്രി ഭക്ഷണം",
        AccommodationMealPlan.MAP => "MAP (രാത്രി ഭക്ഷണം + പ്രഭാതം)",
        AccommodationMealPlan.CP => "CP",
        AccommodationMealPlan.BreakfastOnly => "പ്രഭാതം മാത്രം",
        AccommodationMealPlan.RoomOnly => "EP (മുറി മാത്രം)",
        _ => plan.ToString()
    };

    private static string Tamil(AccommodationMealPlan plan) => plan switch
    {
        AccommodationMealPlan.AP => "காலை + மதியம் + இரவு உணவு",
        AccommodationMealPlan.MAP => "MAP (இரவு உணவு + காலை)",
        AccommodationMealPlan.CP => "CP",
        AccommodationMealPlan.BreakfastOnly => "காலை உணவு மட்டும்",
        AccommodationMealPlan.RoomOnly => "EP (அறை மட்டும்)",
        _ => plan.ToString()
    };

    private static string Telugu(AccommodationMealPlan plan) => plan switch
    {
        AccommodationMealPlan.AP => "అల్పాహారం + మధ్యాహ్న భోజనం + రాత్రి భోజనం",
        AccommodationMealPlan.MAP => "MAP (రాత్రి భోజనం + అల్పాహారం)",
        AccommodationMealPlan.CP => "CP",
        AccommodationMealPlan.BreakfastOnly => "అల్పాహారం మాత్రమే",
        AccommodationMealPlan.RoomOnly => "EP (గది మాత్రమే)",
        _ => plan.ToString()
    };

    private static string Kannada(AccommodationMealPlan plan) => plan switch
    {
        AccommodationMealPlan.AP => "ಉಪಾಹಾರ + ಮಧ್ಯಾಹ್ನ ಊಟ + ರಾತ್ರಿ ಊಟ",
        AccommodationMealPlan.MAP => "MAP (ರಾತ್ರಿ ಊಟ + ಉಪಾಹಾರ)",
        AccommodationMealPlan.CP => "CP",
        AccommodationMealPlan.BreakfastOnly => "ಉಪಾಹಾರ ಮಾತ್ರ",
        AccommodationMealPlan.RoomOnly => "EP (ಕೊಠಡಿ ಮಾತ್ರ)",
        _ => plan.ToString()
    };

    private static string Marathi(AccommodationMealPlan plan) => plan switch
    {
        AccommodationMealPlan.AP => "नाश्ता + दुपारचे जेवण + रात्रीचे जेवण",
        AccommodationMealPlan.MAP => "MAP (रात्रीचे जेवण + नाश्ता)",
        AccommodationMealPlan.CP => "CP",
        AccommodationMealPlan.BreakfastOnly => "फक्त नाश्ता",
        AccommodationMealPlan.RoomOnly => "EP (फक्त खोली)",
        _ => plan.ToString()
    };
}
