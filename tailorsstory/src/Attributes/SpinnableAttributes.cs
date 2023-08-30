using System;
using Vintagestory.API.Common;

namespace tailorsstory
{
  public class SpinnableAttributes
  {
    public readonly string ATTRIBUTE_CODE = "spinnable";

    public bool isSpinnable = false;
    public int inputStackSize;
    public int outputStackSize;
    public string outputCode;
    public EnumItemClass type;

    public SpinnableAttributes(CollectibleObject collectible)
    {
      if (collectible?.Attributes?[ATTRIBUTE_CODE] != null)
      {
        inputStackSize = collectible.Attributes[ATTRIBUTE_CODE]["requiredAmount"].AsInt(4);
        outputStackSize = collectible.Attributes[ATTRIBUTE_CODE]["amount"].AsInt(1);
        outputCode = collectible.Attributes[ATTRIBUTE_CODE]["code"].AsString("");
        type = collectible.Attributes[ATTRIBUTE_CODE]["type"].AsString("") == "block" ? EnumItemClass.Block : EnumItemClass.Item;

        if (outputCode != "")
        {
          isSpinnable = true;
        }
      }
    }

    public JsonItemStack GetJsonItemStack()
    {
      if (!isSpinnable) return null;

      return new JsonItemStack()
      {
        Type = type,
        Code = new AssetLocation(outputCode),
        StackSize = outputStackSize
      };
    }

    public ItemStack GetItemStack(IWorldAccessor resolver)
    {
      if (!isSpinnable) return null;

      JsonItemStack jsonItemStack = GetJsonItemStack();
      bool resolved = jsonItemStack.Resolve(resolver, "spinning");

      return resolved ? jsonItemStack.ResolvedItemstack : null;
    }
  }
}
