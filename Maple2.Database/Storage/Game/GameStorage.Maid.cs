using Maple2.Database.Extensions;
using Maple2.Model.Enum;
using Maid = Maple2.Database.Model.Maid;

namespace Maple2.Database.Storage;

public partial class GameStorage {
    public partial class Request {
        /// <summary>
        /// Loads the maid an account hired, re-pointing it at the pad it is standing on now.
        /// The npc it wears comes from the cube's item metadata rather than the row.
        /// </summary>
        public Maple2.Model.Game.Maid? GetMaid(long accountId, int npcId, long cubeUid) {
            Maid? model = Context.Maid.FirstOrDefault(maid => maid.AccountId == accountId && maid.MaidId == npcId);
            if (model is null) {
                return null;
            }

            if (model.CubeUid != cubeUid) {
                model.CubeUid = cubeUid;
                Context.Maid.Update(model);
                Context.TrySaveChanges();
            }

            return ToMaid(model, npcId);
        }

        /// <summary>
        /// The contract item a maid pad was placed from, with the employment period it carries.
        /// Placing a cube leaves the storage entry behind with its amount at zero so the uid
        /// survives, which is what marks the placed copy; a second copy of the same contract is
        /// not obtainable in normal play.
        /// </summary>
        public (long Uid, long ExpiryTime) GetMaidContract(long ownerId, int itemId) {
            Model.Item? model = Context.Item
                .Where(item => item.OwnerId == ownerId && item.ItemId == itemId && item.Group == ItemGroup.Furnishing)
                .OrderBy(item => item.Amount)
                .FirstOrDefault();

            if (model is null || model.ExpiryTime == default) {
                return (0, 0);
            }

            return (model.Id, model.ExpiryTime.ToEpochSeconds());
        }

        public Maple2.Model.Game.Maid? CreateMaid(long accountId, long cubeUid, int npcId, long hireTime, long expiryTime, int mood) {
            var model = new Maid {
                AccountId = accountId,
                MaidId = npcId,
                CubeUid = cubeUid,
                HireTime = hireTime,
                ExpiryTime = expiryTime,
                PayTime = hireTime,
                ClosenessLevel = 1,
                ClosenessExp = 0,
                ClosenessTime = hireTime,
                Mood = mood,
                MoodTime = hireTime,
            };

            Context.Maid.Add(model);

            return Context.TrySaveChanges() ? ToMaid(model, npcId) : null;
        }

        public bool SaveMaid(Maple2.Model.Game.Maid maid) {
            Maid? model = Context.Maid.Find(maid.Id);
            if (model is null) {
                return false;
            }

            model.HireTime = maid.HireTime;
            model.ExpiryTime = maid.ExpiryTime;
            model.PayTime = maid.PayTime;
            model.ClosenessLevel = maid.ClosenessLevel;
            model.ClosenessExp = maid.ClosenessExp;
            model.ClosenessTime = maid.ClosenessTime;
            model.Mood = maid.Mood;
            model.MoodTime = maid.MoodTime;
            model.CraftRecipeId = maid.Craft?.RecipeId ?? 0;
            model.CraftStartTime = maid.Craft?.StartTime ?? 0;
            model.CraftLeadTime = maid.Craft?.LeadTime ?? 0;
            model.CubeUid = maid.ItemUid;

            Context.Maid.Update(model);

            return Context.TrySaveChanges();
        }

        /// <summary>
        /// Ends an employment for good. Picking the pad up only stores the maid away, so this
        /// is not part of that path.
        /// </summary>
        public bool DeleteMaid(long accountId, int npcId) {
            Maid? model = Context.Maid.FirstOrDefault(maid => maid.AccountId == accountId && maid.MaidId == npcId);
            if (model is null) {
                return false;
            }

            Context.Maid.Remove(model);

            return Context.TrySaveChanges();
        }

        private static Maple2.Model.Game.Maid ToMaid(Maid model, int npcId) {
            var maid = new Maple2.Model.Game.Maid {
                Id = model.Id,
                ItemUid = model.CubeUid,
                AccountId = model.AccountId,
                MaidId = npcId,
                NpcId = npcId,
                Placed = 1,
                HireTime = model.HireTime,
                ExpiryTime = model.ExpiryTime,
                PayTime = model.PayTime,
                ClosenessLevel = model.ClosenessLevel,
                ClosenessExp = model.ClosenessExp,
                ClosenessTime = model.ClosenessTime,
                Mood = model.Mood,
                MoodTime = model.MoodTime,
            };

            if (model.CraftRecipeId > 0) {
                maid.Craft = new Maple2.Model.Game.MaidCraftItem {
                    Id = model.Id,
                    MaidItemUid = model.CubeUid,
                    RecipeId = model.CraftRecipeId,
                    StartTime = model.CraftStartTime,
                    LeadTime = model.CraftLeadTime,
                };
            }

            return maid;
        }
    }
}
