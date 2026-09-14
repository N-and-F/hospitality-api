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
            var menu = new (string Name, string Kind, decimal SellPrice, decimal Cost)[]
            {
                ("Danggit Silog", "plate", 140.00m, 61.83m),
                ("Longsilog", "plate", 120.00m, 48.50m),
                ("Tocilog", "plate", 120.00m, 55.17m),
                ("Hotsilog", "plate", 110.00m, 42.79m),
                ("Hamsilog", "plate", 120.00m, 53.50m),
                ("Cornsilog", "plate", 130.00m, 48.00m),
                ("Danggit", "extra", 50.00m, 33.33m),
                ("Longganisa", "extra", 30.00m, 20.00m),
                ("Tocino", "extra", 30.00m, 26.67m),
                ("Hotdog", "extra", 30.00m, 14.29m),
                ("Ham", "extra", 30.00m, 25.00m),
                ("Corned beef", "extra", 30.00m, 19.50m),
                ("Egg", "extra", 15.00m, 8.50m),
                ("Rice", "extra", 15.00m, 10.00m),
                ("Coffee stick", "extra", 7.00m, 5.00m)
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
                    SortOrder = i
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
