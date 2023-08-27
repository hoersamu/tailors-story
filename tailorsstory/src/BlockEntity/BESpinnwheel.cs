using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace tailorsstory
{

  public class BlockEntitySpinnwheel : BlockEntityOpenableContainer
  {
    internal InventorySpinnwheel inventory;

    // For how long the current fiber has been spinning
    public float inputSpinTime;
    public float prevInputSpinTime;

    GuiDialogBlockEntitySpinnwheel clientDialog;
    SpinnwheelTopRenderer renderer;

    // Server side only
    Dictionary<string, long> playersSpinning = new Dictionary<string, long>();
    // Client and serverside
    int quantityPlayersSpinning;

    int nowOutputFace;

    #region Getters

    public float SpinningSpeed
    {
      get
      {
        if (quantityPlayersSpinning > 0) return 1f;

        return 0;
      }
    }


    MeshData spinnwheelBaseMesh
    {
      get
      {
        object value;
        Api.ObjectCache.TryGetValue("spinnwheelbasemesh", out value);
        return (MeshData)value;
      }
      set { Api.ObjectCache["spinnwheelbasemesh"] = value; }
    }

    MeshData spinnwheelTopMesh
    {
      get
      {
        object value = null;
        Api.ObjectCache.TryGetValue("spinnwheeltopmesh", out value);
        return (MeshData)value;
      }
      set { Api.ObjectCache["spinnwheeltopmesh"] = value; }
    }

    #endregion

    #region Config

    // seconds it requires to spin the spinnable
    public virtual float maxSpinningTime()
    {
      return 4;
    }

    public override string InventoryClassName
    {
      get { return "spinnwheel"; }
    }

    public virtual string DialogTitle
    {
      get { return Lang.Get("tailorsstory:block-spinnwheel"); }
    }

    public override InventoryBase Inventory
    {
      get { return inventory; }
    }

    #endregion


    public BlockEntitySpinnwheel()
    {
      inventory = new InventorySpinnwheel(null, null);
      inventory.SlotModified += OnSlotModifid;
    }



    public override void Initialize(ICoreAPI api)
    {
      base.Initialize(api);

      inventory.LateInitialize("spinnwheel-" + Pos.X + "/" + Pos.Y + "/" + Pos.Z, api);

      RegisterGameTickListener(Every100ms, 100);
      RegisterGameTickListener(Every500ms, 500);

      if (api.Side == EnumAppSide.Client)
      {
        renderer = new SpinnwheelTopRenderer(api as ICoreClientAPI, Pos, GenMesh("top"));

        (api as ICoreClientAPI).Event.RegisterRenderer(renderer, EnumRenderStage.Opaque, "spinnwheel");

        if (spinnwheelBaseMesh == null)
        {
          spinnwheelBaseMesh = GenMesh("base");
        }
        if (spinnwheelTopMesh == null)
        {
          spinnwheelTopMesh = GenMesh("top");
        }
      }
    }

    public void IsSpinning(IPlayer byPlayer)
    {
      SetPlayerSpinning(byPlayer, true);
    }

    private void Every100ms(float dt)
    {
      float spinningSpeed = SpinningSpeed;

      // Only tick on the server and merely sync to client

      // Use up fuel
      if (CanSpin() && spinningSpeed > 0)
      {
        inputSpinTime += dt * spinningSpeed;

        if (inputSpinTime >= maxSpinningTime())
        {
          spinInput();
          inputSpinTime = 0;
        }

        MarkDirty();
      }
    }

    private void spinInput()
    {
      int requiredMaterial = InputSpinnableAttributes.inputStackSize;
      if (InputSlot.Itemstack.StackSize < requiredMaterial) return;
      JsonItemStack jsonItemStack = InputSpinnableAttributes.getJsonItemStack();
      bool resolve = jsonItemStack.Resolve(Api.World, "spinning");
      if (!resolve) return;

      ItemStack spinnedStack = jsonItemStack.ResolvedItemstack;

      if (OutputSlot.Itemstack == null)
      {
        OutputSlot.Itemstack = spinnedStack;
      }
      else
      {
        int mergableQuantity = OutputSlot.Itemstack.Collectible.GetMergableQuantity(OutputSlot.Itemstack, spinnedStack, EnumMergePriority.AutoMerge);

        if (mergableQuantity > 0)
        {
          OutputSlot.Itemstack.StackSize += spinnedStack.StackSize;
        }
        else
        {
          BlockFacing face = BlockFacing.HORIZONTALS[nowOutputFace];
          nowOutputFace = (nowOutputFace + 1) % 4;

          Block block = Api.World.BlockAccessor.GetBlock(Pos.AddCopy(face));
          if (block.Replaceable < 6000) return;
          Api.World.SpawnItemEntity(spinnedStack, Pos.ToVec3d().Add(0.5 + face.Normalf.X * 0.7, 0.75, 0.5 + face.Normalf.Z * 0.7), new Vec3d(face.Normalf.X * 0.02f, 0, face.Normalf.Z * 0.02f));
        }
      }

      InputSlot.TakeOut(requiredMaterial);
      InputSlot.MarkDirty();
      OutputSlot.MarkDirty();
    }


    // Sync to client every 500ms
    private void Every500ms(float dt)
    {
      if (Api.Side == EnumAppSide.Server && (SpinningSpeed > 0 || prevInputSpinTime != inputSpinTime) && InputSpinnableAttributes != null)  //don't spam update packets when empty, as inputSpinTime is irrelevant when empty
      {
        MarkDirty();
      }

      prevInputSpinTime = inputSpinTime;


      foreach (var val in playersSpinning)
      {
        long ellapsedMs = Api.World.ElapsedMilliseconds;
        if (ellapsedMs - val.Value > 1000)
        {
          playersSpinning.Remove(val.Key);
          break;
        }
      }
    }





    public void SetPlayerSpinning(IPlayer player, bool playerSpinning)
    {
      if (playerSpinning)
      {
        playersSpinning[player.PlayerUID] = Api.World.ElapsedMilliseconds;
      }
      else
      {
        playersSpinning.Remove(player.PlayerUID);
      }

      quantityPlayersSpinning = playersSpinning.Count;

      updateSpinningState();
    }

    bool beforeSpinning;
    void updateSpinningState()
    {
      if (Api?.World == null) return;

      bool nowSpinning = quantityPlayersSpinning > 0;

      if (nowSpinning != beforeSpinning)
      {
        if (renderer != null)
        {
          renderer.ShouldRotateManual = quantityPlayersSpinning > 0;
        }

        Api.World.BlockAccessor.MarkBlockDirty(Pos, OnRetesselated);

        if (Api.Side == EnumAppSide.Server)
        {
          MarkDirty();
        }
      }

      beforeSpinning = nowSpinning;
    }




    private void OnSlotModifid(int slotid)
    {
      if (Api is ICoreClientAPI)
      {
        clientDialog?.Update(inputSpinTime, maxSpinningTime());
      }

      if (slotid == 0)
      {
        if (InputSlot.Empty)
        {
          inputSpinTime = 0.0f; // reset the progress to 0 if the item is removed.
        }
        MarkDirty();

        if (clientDialog != null && clientDialog.IsOpened())
        {
          clientDialog.SingleComposer.ReCompose();
        }
      }
    }


    private void OnRetesselated()
    {
      if (renderer == null) return; // Maybe already disposed

      renderer.ShouldRender = quantityPlayersSpinning > 0;
    }




    internal MeshData GenMesh(string type = "base")
    {
      Block block = Api.World.BlockAccessor.GetBlock(Pos);
      if (block.BlockId == 0) return null;

      MeshData mesh;
      ITesselatorAPI mesher = ((ICoreClientAPI)Api).Tesselator;

      mesher.TesselateShape(block, Shape.TryGet(Api, "game:shapes/block/stone/quern/" + type + ".json"), out mesh);

      return mesh;
    }




    public bool CanSpin()
    {
      SpinnableAttributes spinProps = InputSpinnableAttributes;
      if (spinProps == null) return false;
      return true;
    }

    #region Events

    public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
    {
      if (blockSel.SelectionBoxIndex == 1) return false;

      if (Api.Side == EnumAppSide.Client)
      {
        toggleInventoryDialogClient(byPlayer, () =>
        {
          clientDialog = new GuiDialogBlockEntitySpinnwheel(DialogTitle, Inventory, Pos, Api as ICoreClientAPI);
          clientDialog.Update(inputSpinTime, maxSpinningTime());
          return clientDialog;
        });

      }

      return true;
    }


    public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
    {
      base.OnReceivedClientPacket(player, packetid, data);
    }

    public override void OnReceivedServerPacket(int packetid, byte[] data)
    {
      base.OnReceivedServerPacket(packetid, data);

      if (packetid == (int)EnumBlockEntityPacketId.Close)
      {
        (Api.World as IClientWorldAccessor).Player.InventoryManager.CloseInventory(Inventory);
        invDialog?.TryClose();
        invDialog?.Dispose();
        invDialog = null;
      }
    }



    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
      base.FromTreeAttributes(tree, worldForResolving);
      Inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));

      if (Api != null)
      {
        Inventory.AfterBlocksLoaded(Api.World);
      }


      inputSpinTime = tree.GetFloat("inputSpinTime");
      nowOutputFace = tree.GetInt("nowOutputFace");

      if (worldForResolving.Side == EnumAppSide.Client)
      {
        List<int> clientIds = new List<int>((tree["clientIdsSpinning"] as IntArrayAttribute).value);

        quantityPlayersSpinning = clientIds.Count;

        string[] playeruids = playersSpinning.Keys.ToArray();

        foreach (var uid in playeruids)
        {
          IPlayer plr = Api.World.PlayerByUid(uid);

          if (!clientIds.Contains(plr.ClientId))
          {
            playersSpinning.Remove(uid);
          }
          else
          {
            clientIds.Remove(plr.ClientId);
          }
        }

        for (int i = 0; i < clientIds.Count; i++)
        {
          IPlayer plr = worldForResolving.AllPlayers.FirstOrDefault(p => p.ClientId == clientIds[i]);
          if (plr != null) playersSpinning.Add(plr.PlayerUID, worldForResolving.ElapsedMilliseconds);
        }

        updateSpinningState();
      }


      if (Api?.Side == EnumAppSide.Client && clientDialog != null)
      {
        clientDialog.Update(inputSpinTime, maxSpinningTime());
      }
    }



    public override void ToTreeAttributes(ITreeAttribute tree)
    {
      base.ToTreeAttributes(tree);
      ITreeAttribute invtree = new TreeAttribute();
      Inventory.ToTreeAttributes(invtree);
      tree["inventory"] = invtree;

      tree.SetFloat("inputSpinTime", inputSpinTime);
      tree.SetInt("nowOutputFace", nowOutputFace);
      List<int> vals = new List<int>();
      foreach (var val in playersSpinning)
      {
        IPlayer plr = Api.World.PlayerByUid(val.Key);
        if (plr == null) continue;
        vals.Add(plr.ClientId);
      }


      tree["clientIdsSpinning"] = new IntArrayAttribute(vals.ToArray());
    }




    public override void OnBlockRemoved()
    {
      base.OnBlockRemoved();

      clientDialog?.TryClose();

      renderer?.Dispose();
      renderer = null;
    }

    public override void OnBlockBroken(IPlayer byPlayer = null)
    {
      base.OnBlockBroken(byPlayer);
    }

    #endregion

    #region Helper getters


    public ItemSlot InputSlot
    {
      get { return inventory[0]; }
    }

    public ItemSlot OutputSlot
    {
      get { return inventory[1]; }
    }

    public ItemStack InputStack
    {
      get { return inventory[0].Itemstack; }
      set { inventory[0].Itemstack = value; inventory[0].MarkDirty(); }
    }

    public ItemStack OutputStack
    {
      get { return inventory[1].Itemstack; }
      set { inventory[1].Itemstack = value; inventory[1].MarkDirty(); }
    }


    public SpinnableAttributes InputSpinnableAttributes
    {
      get
      {
        ItemSlot slot = inventory[0];
        if (slot.Itemstack == null) return null;
        SpinnableAttributes spinnableAttributes = new SpinnableAttributes(slot.Itemstack.Collectible);
        if (!spinnableAttributes.isSpinnable) return null;

        return spinnableAttributes;
      }
    }

    #endregion


    public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
    {
      foreach (var slot in Inventory)
      {
        if (slot.Itemstack == null) continue;

        if (slot.Itemstack.Class == EnumItemClass.Item)
        {
          itemIdMapping[slot.Itemstack.Item.Id] = slot.Itemstack.Item.Code;
        }
        else
        {
          blockIdMapping[slot.Itemstack.Block.BlockId] = slot.Itemstack.Block.Code;
        }
      }
    }

    public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed)
    {
      foreach (var slot in Inventory)
      {
        if (slot.Itemstack == null) continue;
        if (!slot.Itemstack.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve))
        {
          slot.Itemstack = null;
        }
      }
    }



    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
    {
      if (Block == null) return false;

      mesher.AddMeshData(spinnwheelBaseMesh);
      if (quantityPlayersSpinning == 0)
      {
        mesher.AddMeshData(
            spinnwheelTopMesh.Clone()
            .Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, renderer.AngleRad, 0)
            .Translate(0 / 16f, 11 / 16f, 0 / 16f)
        );
      }


      return true;
    }


    public override void OnBlockUnloaded()
    {
      base.OnBlockUnloaded();

      renderer?.Dispose();
    }

  }
}
