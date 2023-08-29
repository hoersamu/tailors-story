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
    private const int WEAVING_TIME = 4;

    // For how long the current fiber has been spinning
    private float inputWeaveTime;
    private float prevInputWeaveTime;

    // Server side only
    private readonly Dictionary<string, long> playersWeaving = new();
    // Client and serverside
    private int quantityPlayersWeaving;

    readonly InventoryGeneric Inv;
    public override InventoryBase Inventory => Inv;
    public override string InventoryClassName => "gravityloom";
    private ItemSlot ContentSlot { get { return Inv[0]; } }

    private WeavableAttributes WeavableAttributes => new(ContentSlot.Itemstack?.Collectible);

    public BlockEntityGravityLoom()
    {
      Inv = new InventoryGeneric(1, null, null);
    }

    BlockEntityAnimationUtil AnimUtil
    {
      get { return GetBehavior<BEBehaviorAnimatable>().animUtil; }
    }
    readonly AnimationMetaData CompressAnimMeta = new()
    {
      Animation = "weave",
      Code = "weave",
      AnimationSpeed = 1,
      EaseOutSpeed = 1,
      EaseInSpeed = 1
    };

    public override void Initialize(ICoreAPI api)
    {
      base.Initialize(api);
      Inv.LateInitialize(InventoryClassName + "-" + Pos, api);

      RegisterGameTickListener(Every100ms, 100);
      RegisterGameTickListener(Every500ms, 500);

      if (Block != null)
      {
        Shape shape = Shape.TryGet(api, GetShapeForFillLevel(4));


        if (api.Side == EnumAppSide.Client)
        {
          for (int i = 0; i <= 4; i++)
          {
            if (GetMeshDataForFillLevel(i) == null)
            {
              SetMeshDataForFillLevel(GenMeshData(i), i);
            }
          }
          AnimUtil.InitializeAnimator("gravityloom", shape, null, new Vec3f(0, Block.Shape.rotateY, 0));
        }
      }
    }

    private void SetMeshDataForFillLevel(MeshData mesh, int fillLevel)
    {
      Api.ObjectCache["gravityloom-" + fillLevel] = mesh;
    }

    private MeshData GetMeshDataForFillLevel(int fillLevel)
    {
      Api.ObjectCache.TryGetValue("gravityloom-" + fillLevel, out object value);
      return (MeshData)value;
    }

    private MeshData GenMeshData(int fillLevel)
    {
      Block block = Api.World.BlockAccessor.GetBlock(Pos);
      if (block.BlockId == 0) return null;

      ITesselatorAPI mesher = ((ICoreClientAPI)Api).Tesselator;

      Shape shape = Shape.TryGet(Api, GetShapeForFillLevel(fillLevel));
      mesher.TesselateShape(block, shape, out MeshData mesh);

      return mesh;
    }

    private static string GetShapeForFillLevel(int fillLevel)
    {
      return "tailorsstory:shapes/block/loom/gravityloom-" + fillLevel + ".json";
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
          WeaveInput();
          inputWeaveTime = 0;
        }

        MarkDirty();
      }
    }

    private void WeaveInput()
    {
      JsonItemStack jsonItemStack = WeavableAttributes.getJsonItemStack();
      bool resolve = jsonItemStack.Resolve(Api.World, "weaving");
      if (!resolve) return;

      ItemStack weavedStack = jsonItemStack.ResolvedItemstack;

      Api.World.SpawnItemEntity(weavedStack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));

      Inv[0].TakeOutWhole();
      Api.World.BlockAccessor.MarkBlockDirty(Pos);
    }

    // Sync to client every 500ms
    private void Every500ms(float dt)
    {
      if (Api.Side == EnumAppSide.Server && (quantityPlayersWeaving > 0 || prevInputWeaveTime != inputWeaveTime) && WeavableAttributes != null)  //don't spam update packets when empty, as inputSpinTime is irrelevant when empty
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
      UpdateWeavingState();
    }

    bool beforeWeaving;
    private void UpdateWeavingState()
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
      return WeavableAttributes != null && ContentSlot.Itemstack?.StackSize >= WeavableAttributes.inputStackSize;
    }

    public bool OnPlayerInteract(IPlayer byPlayer)
    {
      if (!CanWeave())
      {
        ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (IsValidWeavingMaterial(slot.Itemstack))
        {
          int moved = slot.TryPutInto(Api.World, ContentSlot);
          if (moved > 0)
          {
            AssetLocation sound = slot.Itemstack?.Block?.Sounds?.Place;
            Api.World.PlaySoundAt(sound != null ? sound : new AssetLocation("sounds/player/build"), byPlayer.Entity, byPlayer, true, 16);
            byPlayer.InventoryManager.BroadcastHotbarSlot();
            (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

            MarkDirty();
            Api.World.BlockAccessor.MarkBlockDirty(Pos);
            return true;
          }
        }
      }
      else if (HasWeavingShuttleInHand(byPlayer))
      {
        SetPlayerWeaving(byPlayer, true);
        AnimUtil.StartAnimation(CompressAnimMeta);
        return true;
      }

      return false;
    }

    private static bool IsValidWeavingMaterial(ItemStack stack)
    {
      return new WeavableAttributes(stack?.Collectible).isWeavable;
    }

    private static bool HasWeavingShuttleInHand(IPlayer byPlayer)
    {
      return byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Collectible.Code.Path.StartsWith("weavingshuttle");
    }

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
    {
      if (Block == null) return false;

      mesher.AddMeshData(GetMeshDataForFillLevel(ContentSlot.Itemstack?.StackSize ?? 0));

      return true;
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
      base.FromTreeAttributes(tree, worldForResolving);
      base.FromTreeAttributes(tree, worldForResolving);
      Inv.FromTreeAttributes(tree.GetTreeAttribute("inventory"));

      inputWeaveTime = tree.GetFloat("inputSpinTime");

      if (worldForResolving.Side == EnumAppSide.Client)
      {
        List<int> clientIds = new((tree["clientIdsSpinning"] as IntArrayAttribute).value);

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

        UpdateWeavingState();
      }
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
      base.ToTreeAttributes(tree);

      ITreeAttribute invtree = new TreeAttribute();
      Inv.ToTreeAttributes(invtree);
      tree["inventory"] = invtree;

      tree.SetFloat("inputSpinTime", inputWeaveTime);
      List<int> vals = new();
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
