using Vintagestory.API.Common;

namespace tailorsstory
{
    public class tailorsstoryModSystem : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterItemClass("ItemHandspindle", typeof(ItemHandspindle));
            api.RegisterBlockClass("BlockSpinnwheel", typeof(BlockSpinnwheel));
            api.RegisterBlockEntityClass("BlockEntitySpinnwheel", typeof(BlockEntitySpinnwheel));

        }
    }
}
