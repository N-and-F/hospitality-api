using Hospitality.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Hospitality.Api.Felogan;

public static class FeloganSeed
{
    public static async Task EnsureAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (!await db.FeloganRooms.AnyAsync(cancellationToken))
        {
            var rooms = new[] { "F1", "F2", "F3", "F4", "N2", "301", "302", "303", "304", "305", "306" };
            for (var i = 0; i < rooms.Length; i++)
            {
                db.FeloganRooms.Add(new FeloganRoom
                {
                    Id = Guid.NewGuid(),
                    Name = rooms[i],
                    SortOrder = i
                });
            }
        }

        if (!await db.FeloganCooks.AnyAsync(cancellationToken))
        {
            var cooks = new[] { "Lisa", "Melly" };
            for (var i = 0; i < cooks.Length; i++)
            {
                db.FeloganCooks.Add(new FeloganCook
                {
                    Id = Guid.NewGuid(),
                    Name = cooks[i],
                    SortOrder = i
                });
            }
        }

        if (!await db.FeloganMenuItems.AnyAsync(cancellationToken))
        {
            var menu = new (string Name, string Kind, decimal SellPrice, decimal Cost, int OpeningPacks)[]
            {
                ("Danggit Silog", "plate", 140.00m, 61.83m, 1),
                ("Longsilog", "plate", 120.00m, 48.50m, 7),
                ("Tocilog", "plate", 120.00m, 55.17m, 3),
                ("Hotsilog", "plate", 110.00m, 42.79m, 28),
                ("Hamsilog", "plate", 120.00m, 53.50m, 12),
                ("Cornsilog", "plate", 130.00m, 48.00m, 5),
                ("Danggit", "extra", 50.00m, 33.33m, 1),
                ("Longganisa", "extra", 30.00m, 20.00m, 7),
                ("Tocino", "extra", 30.00m, 26.67m, 3),
                ("Hotdog", "extra", 30.00m, 14.29m, 28),
                ("Ham", "extra", 30.00m, 25.00m, 12),
                ("Corned beef", "extra", 30.00m, 19.50m, 5),
                ("Egg", "extra", 15.00m, 8.50m, -7),
                ("Rice", "extra", 15.00m, 10.00m, 0),
                ("Coffee stick", "extra", 7.00m, 5.00m, 0),
                ("Paper cups", "stock", 0m, 0m, 0),
                ("Fork", "stock", 0m, 0m, 0),
                ("Spoon", "stock", 0m, 0m, 0),
                ("Canton", "stock", 0m, 0m, 0)
            };

            for (var i = 0; i < menu.Length; i++)
            {
                db.FeloganMenuItems.Add(new FeloganMenuItem
                {
                    Id = Guid.NewGuid(),
                    Name = menu[i].Name,
                    Kind = menu[i].Kind,
                    SellPrice = menu[i].SellPrice,
                    Cost = menu[i].Cost,
                    OpeningPacks = menu[i].OpeningPacks,
                    SortOrder = i
                });
            }
        }

        if (!await db.FeloganSettings.AnyAsync(cancellationToken))
        {
            db.FeloganSettings.Add(new FeloganSettings
            {
                Id = Guid.NewGuid(),
                CookPercent = 40
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
