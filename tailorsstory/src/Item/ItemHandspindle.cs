using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace tailorsstory
{
  public class ItemHandspindle : Item
  {
    private readonly int SPIN_TIME_IN_SECONDS = 3;
    private CollectibleObject SpinnableObject;

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
      CollectibleObject spinnableObject = GetSpinnableObject(byEntity as EntityPlayer);
      if (spinnableObject != null)
      {
        SpinnableObject = spinnableObject;
        handling = EnumHandHandling.Handled;
      }
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {

      int renderVariant = GameMath.Clamp((int)Math.Ceiling(secondsUsed / SPIN_TIME_IN_SECONDS * 4), 0, 4);
      int prevRenderVariant = slot.Itemstack.Attributes.GetInt("renderVariant", 0);

      slot.Itemstack.TempAttributes.SetInt("renderVariant", renderVariant);
      slot.Itemstack.Attributes.SetInt("renderVariant", renderVariant);

      if (prevRenderVariant != renderVariant)
      {
        (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();
      }


      if (secondsUsed > SPIN_TIME_IN_SECONDS)
      {
        SpinInput(byEntity as EntityPlayer);
        return false;
      }

      return true;
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
      SpinnableObject = null;

      if (slot.Itemstack.Attributes.GetInt("renderVariant", 0) != 0)
      {
        slot.Itemstack.TempAttributes.SetInt("renderVariant", 0);
        slot.Itemstack.Attributes.SetInt("renderVariant", 0);
        (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();
      }
    }

    private void SpinInput(EntityPlayer byPlayer)
    {
      if (SpinnableObject == null) return;

      SpinnableAttributes spinnableAttributes = new SpinnableAttributes(SpinnableObject);

      int cost = spinnableAttributes.inputStackSize;

      byPlayer.WalkInventory((invslot) =>
      {
        if (invslot.Itemstack != null && invslot.Itemstack.Id.Equals(SpinnableObject.Id))
        {
          int takeOutCount = Math.Min(cost, invslot.StackSize);
          invslot.TakeOut(takeOutCount);
          cost -= takeOutCount;

          if (cost <= 0) return false;
        }
        return true;
      });

      ItemStack spunStack = spinnableAttributes.GetItemStack(byPlayer.World);
      if (spunStack == null) return;

      if (byPlayer.TryGiveItemStack(spunStack) == false)
      {
        byPlayer.World.SpawnItemEntity(spunStack, byPlayer.SidedPos.XYZ);
      }
    }

    private static int GetItemCountForItemId(EntityPlayer byPlayer, int itemId)
    {
      int count = 0;

      byPlayer.WalkInventory((invslot) =>
      {
        if (invslot.Itemstack != null && invslot.Itemstack.Id == itemId)
        {
          count += invslot.Itemstack.StackSize;
        }

        return true;
      });

      return count;
    }

    private static CollectibleObject GetSpinnableObject(EntityPlayer byPlayer)
    {
      HashSet<int> alreadyCheckedItems = new();
      CollectibleObject spinnableObject = null;

      byPlayer.WalkInventory((invslot) =>
      {
        if (invslot.Itemstack != null && !alreadyCheckedItems.Contains(invslot.Itemstack.Collectible.Id))
        {
          SpinnableAttributes spinnableAttributes = new(invslot.Itemstack.Collectible);

          if (spinnableAttributes.isSpinnable)
          {
            int countInInventory = GetItemCountForItemId(byPlayer, invslot.Itemstack.Collectible.Id);
            if (countInInventory >= spinnableAttributes.inputStackSize)
            {
              spinnableObject = invslot.Itemstack.Collectible;
              return false;
            }
          }

          alreadyCheckedItems.Add(invslot.Itemstack.Collectible.Id);
        }

        return true;
      });

      return spinnableObject;
    }
  }
}
