using Vintagestory.API.Common;

namespace TailorsStory
{
  public class Tailorsstory : ModSystem
  {

    public override void Start(ICoreAPI api)
    {
      base.Start(api);
      api.RegisterItemClass("ItemHandspindle", typeof(ItemHandspindle));
    }

  }
}
