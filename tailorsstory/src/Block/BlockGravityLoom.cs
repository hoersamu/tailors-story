using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace tailorsstory
{
  public class BlockGravityLoom : Block
  {
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
      if (blockSel != null && !world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
      {
        return false;
      }

      BlockEntityGravityLoom beGravityLoom = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityGravityLoom;

      if (beGravityLoom != null)
      {
        return beGravityLoom.OnPlayerInteract(byPlayer);
      }

      return false;
    }

    public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
      BlockEntityGravityLoom beGravityLoom = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityGravityLoom;

      if (beGravityLoom != null)
      {
        beGravityLoom.IsWeaving(byPlayer);
        return beGravityLoom.CanWeave();
      }

      return false;
    }

    public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
      BlockEntityGravityLoom beGravityLoom = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityGravityLoom;
      if (beGravityLoom != null)
      {
        beGravityLoom.SetPlayerWeaving(byPlayer, false);
      }

    }

    public override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
    {
      BlockEntityGravityLoom beGravityLoom = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityGravityLoom;
      if (beGravityLoom != null)
      {
        beGravityLoom.SetPlayerWeaving(byPlayer, false);
      }

      return true;
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
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


