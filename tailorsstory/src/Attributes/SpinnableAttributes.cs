using Vintagestory.API.Common;

namespace tailorsstory
{
  public class SpinnableAttributes
  {
    public bool isSpinnable = false;
    public int inputStackSize;
    public int outputStackSize;
    public string outputCode;
    public string type;

    public SpinnableAttributes(CollectibleObject collectible)
    {
      if (collectible.Attributes?["spinnable"] != null)
      {
        inputStackSize = collectible.Attributes["spinnable"]["requiredAmount"].AsInt(4);
        outputStackSize = collectible.Attributes["spinnable"]["amount"].AsInt(1);
        outputCode = collectible.Attributes["spinnable"]["code"].AsString("");
        type = collectible.Attributes["spinnable"]["type"].AsString("");

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