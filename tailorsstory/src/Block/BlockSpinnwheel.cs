using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace tailorsstory
{
  public class BlockSpinnwheel : Block
  {
    public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
    {
      return true;
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
      if (blockSel != null && !world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
      {
        return false;
      }

      BlockEntitySpinnwheel beSpinnwheel = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntitySpinnwheel;

      if (beSpinnwheel != null && beSpinnwheel.CanSpin() && (blockSel.SelectionBoxIndex == 1 || beSpinnwheel.Inventory.openedByPlayerGUIds.Contains(byPlayer.PlayerUID)))
      {
        beSpinnwheel.SetPlayerSpinning(byPlayer, true);
        return true;
      }

      return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }

    public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
      BlockEntitySpinnwheel beSpinnwheel = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntitySpinnwheel;

      if (beSpinnwheel != null && (blockSel.SelectionBoxIndex == 1 || beSpinnwheel.Inventory.openedByPlayerGUIds.Contains(byPlayer.PlayerUID)))
      {
        beSpinnwheel.IsSpinning(byPlayer);
        return beSpinnwheel.CanSpin();
      }

      return false;
    }

    public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
      BlockEntitySpinnwheel beSpinnwheel = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntitySpinnwheel;
      if (beSpinnwheel != null)
      {
        beSpinnwheel.SetPlayerSpinning(byPlayer, false);
      }

    }

    public override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
    {
      BlockEntitySpinnwheel beSpinnwheel = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntitySpinnwheel;
      if (beSpinnwheel != null)
      {
        beSpinnwheel.SetPlayerSpinning(byPlayer, false);
      }


      return true;
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
    {
      if (selection.SelectionBoxIndex == 0)
      {
        return new WorldInteraction[] {
          new WorldInteraction()
          {
              ActionLangCode = "blockhelp-quern-addremoveitems",
              MouseButton = EnumMouseButton.Right
          }
        }.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
      }
      else
      {
        return new WorldInteraction[] {
          new WorldInteraction()
          {
            ActionLangCode = "tailorsstory:blockhelp-spinnwheel-spin",
            MouseButton = EnumMouseButton.Right,
            ShouldApply = (wi, bs, es) => {
                BlockEntitySpinnwheel beSpinnwheel = world.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntitySpinnwheel;
                return beSpinnwheel != null && beSpinnwheel.CanSpin();
            }
          }
      }.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
      }
    }
  }
}
