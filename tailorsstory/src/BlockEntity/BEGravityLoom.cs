using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace tailorsstory
{
  public class BlockEntityGravityLoom : BlockEntityContainer
  {
    private readonly int WEAVING_TIME = 4;

    // For how long the current fiber has been spinning
    public float inputWeaveTime;
    public float prevInputWeaveTime;

    // Server side only
    Dictionary<string, long> playersWeaving = new Dictionary<string, long>();
    // Client and serverside
    int quantityPlayersWeaving;

    InventoryGeneric inv;
    public override InventoryBase Inventory => inv;
    public override string InventoryClassName => "gravityloom";
    ItemSlot contentSlot { get { return inv[0]; } }
    WeavableAttributes weavableAttributes => new WeavableAttributes(contentSlot.Itemstack?.Collectible);

    public BlockEntityGravityLoom()
    {
      inv = new InventoryGeneric(1, null, null);
    }

    public override void Initialize(ICoreAPI api)
    {
      base.Initialize(api);
      inv.LateInitialize(InventoryClassName + "-" + Pos, api);

      RegisterGameTickListener(Every100ms, 100);
      RegisterGameTickListener(Every500ms, 500);
    }

    public void IsWeaving(IPlayer byPlayer)
    {
      SetPlayerWeaving(byPlayer, true);
    }

    private void Every100ms(float dt)
    {
      // Only tick on the server and merely sync to client
      if (Api.Side == EnumAppSide.Client) return; ;

      // Use up fuel
      if (CanWeave() && quantityPlayersWeaving > 0)
      {
        inputWeaveTime += dt;

        if (inputWeaveTime >= WEAVING_TIME)
        {
          weaveInput();
          inputWeaveTime = 0;
        }

        MarkDirty();
      }
    }

    private void weaveInput()
    {
      JsonItemStack jsonItemStack = weavableAttributes.getJsonItemStack();
      bool resolve = jsonItemStack.Resolve(Api.World, "weaving");
      if (!resolve) return;

      ItemStack weavedStack = jsonItemStack.ResolvedItemstack;

      Api.World.SpawnItemEntity(weavedStack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));

      inv[0].TakeOutWhole();
      MarkDirty();
    }

    // Sync to client every 500ms
    private void Every500ms(float dt)
    {
      if (Api.Side == EnumAppSide.Server && (quantityPlayersWeaving > 0 || prevInputWeaveTime != inputWeaveTime) && weavableAttributes != null)  //don't spam update packets when empty, as inputSpinTime is irrelevant when empty
      {
        MarkDirty();
      }

      prevInputWeaveTime = inputWeaveTime;


      foreach (var val in playersWeaving)
      {
        long ellapsedMs = Api.World.ElapsedMilliseconds;
        if (ellapsedMs - val.Value > 1000)
        {
          playersWeaving.Remove(val.Key);
          break;
        }
      }
    }

    public void SetPlayerWeaving(IPlayer player, bool playerWeaving)
    {
      if (playerWeaving)
      {
        playersWeaving[player.PlayerUID] = Api.World.ElapsedMilliseconds;
      }
      else
      {
        playersWeaving.Remove(player.PlayerUID);
      }

      quantityPlayersWeaving = playersWeaving.Count;
      updateWeavingState();
    }

    bool beforeWeaving;
    void updateWeavingState()
    {
      if (Api?.World == null) return;

      bool nowSpinning = quantityPlayersWeaving > 0;

      if (nowSpinning != beforeWeaving)
      {

        if (Api.Side == EnumAppSide.Server)
        {
          MarkDirty();
        }
      }

      beforeWeaving = nowSpinning;
    }

    public bool CanWeave()
    {
      return weavableAttributes != null && contentSlot.Itemstack?.StackSize >= weavableAttributes.inputStackSize;
    }

    public bool OnPlayerInteract(IPlayer byPlayer)
    {
      if (!CanWeave())
      {
        ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (isValidWeavingMaterial(slot.Itemstack))
        {
          int moved = slot.TryPutInto(Api.World, contentSlot);
          if (moved > 0)
          {
            MarkDirty();
            AssetLocation sound = slot.Itemstack?.Block?.Sounds?.Place;
            Api.World.PlaySoundAt(sound != null ? sound : new AssetLocation("sounds/player/build"), byPlayer.Entity, byPlayer, true, 16);
            byPlayer.InventoryManager.BroadcastHotbarSlot();
            (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

            return true;
          }
        }
      }
      else if (hasWeavingShuttleInHand(byPlayer))
      {
        SetPlayerWeaving(byPlayer, true);
        return true;
      }

      return false;
    }

    private bool isValidWeavingMaterial(ItemStack stack)
    {
      return new WeavableAttributes(stack?.Collectible).isWeavable;
    }

    private bool hasWeavingShuttleInHand(IPlayer byPlayer)
    {
      return byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Collectible.Code.Path.StartsWith("weavingshuttle");
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
      base.FromTreeAttributes(tree, worldForResolving);
      base.FromTreeAttributes(tree, worldForResolving);
      inv.FromTreeAttributes(tree.GetTreeAttribute("inventory"));

      inputWeaveTime = tree.GetFloat("inputSpinTime");

      if (worldForResolving.Side == EnumAppSide.Client)
      {
        List<int> clientIds = new List<int>((tree["clientIdsSpinning"] as IntArrayAttribute).value);

        quantityPlayersWeaving = clientIds.Count;

        string[] playeruids = playersWeaving.Keys.ToArray();

        foreach (var uid in playeruids)
        {
          IPlayer plr = Api.World.PlayerByUid(uid);

          if (!clientIds.Contains(plr.ClientId))
          {
            playersWeaving.Remove(uid);
          }
          else
          {
            clientIds.Remove(plr.ClientId);
          }
        }

        for (int i = 0; i < clientIds.Count; i++)
        {
          IPlayer plr = worldForResolving.AllPlayers.FirstOrDefault(p => p.ClientId == clientIds[i]);
          if (plr != null) playersWeaving.Add(plr.PlayerUID, worldForResolving.ElapsedMilliseconds);
        }

        updateWeavingState();
      }
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
      base.ToTreeAttributes(tree);

      ITreeAttribute invtree = new TreeAttribute();
      inv.ToTreeAttributes(invtree);
      tree["inventory"] = invtree;

      tree.SetFloat("inputSpinTime", inputWeaveTime);
      List<int> vals = new List<int>();
      foreach (var val in playersWeaving)
      {
        IPlayer plr = Api.World.PlayerByUid(val.Key);
        if (plr == null) continue;
        vals.Add(plr.ClientId);
      }

      tree["clientIdsSpinning"] = new IntArrayAttribute(vals.ToArray());
    }

    public override void OnBlockBroken(IPlayer byPlayer = null)
    {
      base.OnBlockBroken(byPlayer);
    }
  }
}
