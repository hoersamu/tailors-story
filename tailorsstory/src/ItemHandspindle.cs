using System;
using Vintagestory.API.Common;

namespace TailorsStory
{
  public class ItemHandspindle : Item
  {
    private readonly float TIME_TO_SPIN = 3f;

    protected int getFlaxfibreCount(EntityAgent byEntity)
    {
      int count = 0;
      byEntity.WalkInventory((invslot) =>
      {
        if (invslot.Itemstack != null && invslot.Itemstack.Collectible.Code.Path.StartsWith("flaxfibers"))
        {
          count += invslot.Itemstack.StackSize;
        }

        return true;
      });

      return count;
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
      if (getFlaxfibreCount(byEntity) > 4) handling = EnumHandHandling.Handled;
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
      if (secondsUsed > TIME_TO_SPIN)
      {
        if (getFlaxfibreCount(byEntity) < 4) return false;

        IWorldAccessor world = byEntity.World;

        IPlayer byPlayer = null;
        if (byEntity is EntityPlayer) byPlayer = world.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);

        int flaxfibersCost = 4;
        byEntity.WalkInventory((invslot) =>
        {
          if (invslot.Itemstack != null && invslot.Itemstack.Collectible.Code.Path.Equals("flaxfibers"))
          {
            int takeOutCount = Math.Min(flaxfibersCost, invslot.StackSize);
            invslot.TakeOut(takeOutCount);
            flaxfibersCost -= takeOutCount;

            if (flaxfibersCost <= 0) return false;
          }
          return true;
        });

        ItemStack stack = new ItemStack(world.GetItem(new AssetLocation("flaxtwine")), 4);

        if (byPlayer?.InventoryManager.TryGiveItemstack(stack) == false)
        {
          byEntity.World.SpawnItemEntity(stack, byEntity.SidedPos.XYZ);
        }

        return false;
      }

      return true;
    }
  }
}
