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
    public string type;

    public SpinnableAttributes(CollectibleObject collectible)
    {
      if (collectible.Attributes?[ATTRIBUTE_CODE] != null)
      {
        inputStackSize = collectible.Attributes[ATTRIBUTE_CODE]["requiredAmount"].AsInt(4);
        outputStackSize = collectible.Attributes[ATTRIBUTE_CODE]["amount"].AsInt(1);
        outputCode = collectible.Attributes[ATTRIBUTE_CODE]["code"].AsString("");
        type = collectible.Attributes[ATTRIBUTE_CODE]["type"].AsString("");

        if (outputCode != "" && type != "")
        {
          isSpinnable = true;
        }
      }
    }

    public JsonItemStack getJsonItemStack()
    {
      if (!isSpinnable) return null;

      return new JsonItemStack()
      {
        Type = EnumItemClass.Item,
        Code = new AssetLocation(outputCode),
        StackSize = outputStackSize
      };
    }
  }
}
