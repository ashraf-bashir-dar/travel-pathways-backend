using Microsoft.EntityFrameworkCore;
using TravelPathways.Api.Data.Entities;

namespace TravelPathways.Api.Data;

public static class SeedStateCity
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.States.AnyAsync(ct))
            return;

        var statesWithCities = new (string StateName, string? Code, int Order, string[] Cities)[]
        {
            ("Jammu & Kashmir", "JK", 1, new[] { "Srinagar", "Jammu", "Gulmarg", "Pahalgam", "Sonamarg", "Anantnag", "Baramulla", "Sopore", "Kupwara", "Budgam", "Ganderbal", "Bandipora", "Kulgam", "Shopian", "Pulwama", "Katra", "Udhampur", "Kathua", "Samba", "Rajouri", "Poonch", "Doda", "Kishtwar", "Ramban", "Reasi" }),
            ("Ladakh", "LA", 2, new[] { "Leh", "Kargil", "Nubra Valley", "Pangong", "Diskit", "Drass", "Padum" }),
            ("Punjab", "PB", 3, new[] { "Amritsar", "Ludhiana", "Jalandhar", "Patiala", "Bathinda", "Mohali", "Pathankot", "Hoshiarpur", "Batala", "Moga", "Abohar", "Malerkotla", "Khanna", "Phagwara", "Muktsar", "Barnala", "Rajpura", "Firozpur", "Sangrur", "Faridkot", "Fazilka", "Gurdaspur", "Zirakpur" }),
            ("Himachal Pradesh", "HP", 4, new[] { "Shimla", "Manali", "Dharamshala", "Dalhousie", "Kullu", "Mandi", "Solan", "Palampur", "Kasauli", "Chamba", "Kangra", "Una", "Hamirpur", "Bilaspur", "Nahan", "Kufri", "McLeod Ganj", "Spiti Valley", "Kinnaur", "Narkanda", "Rampur" }),
            ("Haryana", "HR", 5, new[] { "Gurgaon", "Faridabad", "Panipat", "Ambala", "Yamunanagar", "Rohtak", "Hisar", "Karnal", "Sonipat", "Panchkula", "Bhiwani", "Sirsa", "Bahadurgarh", "Jind", "Thanesar", "Kaithal", "Rewari", "Palwal", "Nuh", "Mahendragarh", "Narnaul", "Fatehabad" }),
            ("Delhi", "DL", 6, new[] { "New Delhi", "Central Delhi", "North Delhi", "South Delhi", "East Delhi", "West Delhi", "North East Delhi", "North West Delhi", "South West Delhi", "Shahdara", "Dwarka", "Rohini", "Karol Bagh", "Saket", "Hauz Khas", "Connaught Place", "Chandni Chowk", "Lajpat Nagar", "Nehru Place", "Janakpuri" }),
            ("Uttar Pradesh", "UP", 7, new[] { "Lucknow", "Agra", "Varanasi", "Allahabad", "Kanpur", "Mathura", "Vrindavan", "Ayodhya", "Jhansi", "Meerut", "Ghaziabad", "Noida", "Greater Noida", "Bareilly", "Aligarh", "Moradabad", "Saharanpur", "Gorakhpur", "Faizabad", "Firozabad", "Muzaffarnagar", "Sitapur", "Shahjahanpur" }),
            ("Rajasthan", "RJ", 8, new[] { "Jaipur", "Jodhpur", "Udaipur", "Jaisalmer", "Pushkar", "Ajmer", "Bikaner", "Mount Abu", "Kota", "Bundi", "Chittorgarh", "Ranthambore", "Sawai Madhopur", "Alwar", "Bharatpur", "Sikar", "Pali", "Sri Ganganagar", "Hanumangarh", "Tonk", "Kishangarh", "Beawar", "Nagaur", "Dausa", "Dholpur", "Karauli", "Sirohi", "Pratapgarh", "Banswara", "Dungarpur", "Jhalawar", "Baran", "Bhilwara", "Rajsamand", "Churu", "Jhunjhunu" })
        };

        foreach (var (stateName, code, order, cityNames) in statesWithCities)
        {
            var state = new State
            {
                Name = stateName,
                Code = code,
                DisplayOrder = order
            };
            db.States.Add(state);
            await db.SaveChangesAsync(ct);

            foreach (var cityName in cityNames)
            {
                db.Cities.Add(new City
                {
                    Name = cityName,
                    StateId = state.Id
                });
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
