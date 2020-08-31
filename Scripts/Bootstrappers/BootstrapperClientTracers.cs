namespace CryoFall.Tracers
{
    using AtomicTorch.CBND.GameApi.Scripting;
    using AtomicTorch.CBND.GameApi.Data;
    using AtomicTorch.CBND.GameApi.Data.Characters;

    class BootstrapperClientTracers : BaseBootstrapper
    {
        public override void ClientInitialize()
        {
            Api.Client.World.ObjectEnterScope += World_ObjectEnterScope;
        }
        private void World_ObjectEnterScope(IGameObjectWithProto obj)
        {
            if (obj.GameObjectType == GameObjectType.Character)
            {
                var character = (ICharacter)obj;
                ComponentCharacterTracer.Init(character);
            }
        }
    }
}
