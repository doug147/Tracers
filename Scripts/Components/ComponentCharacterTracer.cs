namespace CryoFall.Tracers
{
    using System;
    using System.Windows.Media;
    using System.Linq;
    using System.Collections.Generic;
    using AtomicTorch.GameEngine.Common.Primitives;
    using AtomicTorch.GameEngine.Common.Extensions;
    using AtomicTorch.CBND.GameApi.ServicesClient.Rendering;
    using AtomicTorch.CBND.GameApi.Scripting.ClientComponents;
    using AtomicTorch.CBND.GameApi.Resources;
    using AtomicTorch.CBND.GameApi.Data.Characters;
    using AtomicTorch.CBND.CoreMod.Vehicles;
    using AtomicTorch.CBND.CoreMod.Systems.Weapons;
    using AtomicTorch.CBND.CoreMod.Systems.Party;
    using AtomicTorch.CBND.CoreMod.Items.Weapons;
    using AtomicTorch.CBND.CoreMod.Items.Ammo;
    using AtomicTorch.CBND.CoreMod.CharacterSkeletons;
    using AtomicTorch.CBND.CoreMod.Characters;
    using AtomicTorch.CBND.CoreMod.Characters.Player;

    class ComponentCharacterTracer : ClientComponent
    {
        private static readonly TextureResource TextureResourceBeam
            = new TextureResource("FX/WeaponTraces/BeamLaser.png");
        public static ComponentCharacterTracer Instance { get; set; }
        private ICharacter character;
        private double rangeMax = 5.0;
        public static void Init(ICharacter character)
        {
            Instance = Client.Scene.CreateSceneObject(nameof(ComponentCharacterTracer)).AddComponent<ComponentCharacterTracer>();
            Instance.character = character;
        }
        private Vector2D GetTracerLocation()
        {
            try
            {
                var boneWorldPosition = character.Position + (0, character.ProtoCharacter.CharacterWorldWeaponOffsetRanged);
                var muzzleFlashTextureOffset = Vector2F.Zero;
                var protoWeapon = character.GetPublicState<PlayerCharacterPublicState>().SelectedItemWeaponProto;
                if (protoWeapon is IProtoItemWeaponRanged protoItemWeaponRanged)
                {
                    muzzleFlashTextureOffset = protoItemWeaponRanged.MuzzleFlashDescription.TextureScreenOffset.ToVector2F();
                    var protoSkeleton = character.GetClientState<BaseCharacterClientState>().CurrentProtoSkeleton;
                    var skeleton = (ProtoCharacterSkeleton)protoSkeleton;
                    var slotName = skeleton.SlotNameItemInHand;
                    var clientState = character.GetClientState<BaseCharacterClientState>();
                    var skeletonRenderer = clientState.SkeletonRenderer;
                    var slotOffset = skeletonRenderer.GetSlotScreenOffset(attachmentName: slotName);
                    boneWorldPosition = skeletonRenderer.TransformSlotPosition(
                        slotName,
                        slotOffset + muzzleFlashTextureOffset,
                        out _);
                    return boneWorldPosition;
                }
                return character.Position + (0, character.ProtoCharacter.CharacterWorldWeaponOffsetRanged);
            }
            catch { }

            return character.Position + (0, character.ProtoCharacter.CharacterWorldWeaponOffsetRanged);
        }

        public void LineTraceForHitResults(Vector2D fromPosition, Vector2D toPosition, ref bool isBlocking, ref Vector2D hitPosition)
        {
            var protoWeapon = character.GetPublicState<PlayerCharacterPublicState>().SelectedItemWeaponProto;
            var collisionGroup = protoWeapon.CollisionGroup;
            using var lineTestResults = character.PhysicsBody.PhysicsSpace.TestLine(
                fromPosition: fromPosition,
                toPosition: toPosition,
                collisionGroup: collisionGroup);
            var hitObjects = new List<WeaponHitData>(lineTestResults.Count);
            var characterTileHeight = character.Tile.Height;
            foreach (var testResult in lineTestResults.AsList())
            {
                var testResultPhysicsBody = testResult.PhysicsBody;
                var damagedObject = testResultPhysicsBody.AssociatedWorldObject;
                if (ReferenceEquals(damagedObject, character) || ReferenceEquals(damagedObject, character.SharedGetCurrentVehicle())) { continue; }
                if (!(damagedObject?.ProtoGameObject is IDamageableProtoWorldObject damageableProto)) { continue; }
                if (damagedObject?.ProtoGameObject is PlayerCharacter || damagedObject?.ProtoGameObject is IProtoVehicle)
                {
                    hitPosition = testResult.PhysicsBody.Position + testResult.Penetration;
                    isBlocking = true;
                    break;
                }
            }
        }
        public override void Update(double deltaTime)
        {
            var partyMembers = PartySystem.ClientGetCurrentPartyMembers();
            if (character.IsInitialized && !character.IsDestroyed
                && character.ProtoGameObject is PlayerCharacter
                && !character.GetPublicState<ICharacterPublicState>().IsDead
                && character.GetPublicState<PlayerCharacterPublicState>().SelectedItemWeaponProto != null
                && character.GetPublicState<PlayerCharacterPublicState>().SelectedItemWeaponProto is IProtoItemWeaponRanged
                && (!partyMembers.Contains(character.Name) || character.IsCurrentClientCharacter))
            {
                var beamColor = Colors.Red;
                if (partyMembers.Contains(character.Name) || character.IsCurrentClientCharacter) { beamColor = Colors.Green; }
                var protoWeapon = character.GetPublicState<ICharacterPublicState>().SelectedItemWeaponProto;
                var ammoMaxRange = character.GetPublicState<PlayerCharacterPublicState>().SelectedItemWeaponProto?.CompatibleAmmoProtos?
                    .Max<IProtoItemAmmo>(obj => obj.DamageDescription.RangeMax)
                        ?? protoWeapon?.OverrideDamageDescription?.RangeMax ?? 0.0;
                var rangeMultiplier = character.GetPublicState<ICharacterPublicState>().SelectedItemWeaponProto?.RangeMultiplier ?? 0.0;
                var rangeMax = ammoMaxRange * rangeMultiplier;
                var rangedPosition = character.Position + (0, character.ProtoCharacter.CharacterWorldWeaponOffsetRanged);
                var toPosition = rangedPosition
                    + new Vector2D(rangeMax, 0)
                        .RotateRad(character.ProtoCharacter.SharedGetRotationAngleRad(character));
                bool isBlocking = false;
                var hitPosition = Vector2D.Zero;
                LineTraceForHitResults(GetTracerLocation(), toPosition, ref isBlocking, ref hitPosition);
                var tmpToPosition = Vector2D.Zero;
                if (isBlocking)
                {
                    tmpToPosition = toPosition;
                    toPosition = hitPosition;
                }

                if (rangeMax > 0)
                {
                    ComponentTracerBeam.Create(
                        sourcePosition: GetTracerLocation(),
                        targetPosition: toPosition,
                        traceStartWorldOffset: 0.1,
                        texture: TextureResourceBeam,
                        beamWidth: 0.12,
                        originOffset: Vector2D.Zero,
                        true,
                        fadeInDistance: 0.0,
                        fadeOutDistanceHit: 0.0,
                        fadeOutDistanceNoHit: 0.0,
                        blendMode: BlendMode.AdditivePremultiplied,
                        beamColor);

                    if (isBlocking)
                    {
                        ComponentTracerBeam.Create(
                            sourcePosition: toPosition,
                            targetPosition: tmpToPosition,
                            traceStartWorldOffset: 0.1,
                            texture: TextureResourceBeam,
                            beamWidth: 0.12,
                            originOffset: Vector2D.Zero,
                            true,
                            fadeInDistance: 0.0,
                            fadeOutDistanceHit: 0.0,
                            fadeOutDistanceNoHit: 0.0,
                            blendMode: BlendMode.AdditivePremultiplied,
                            beamColor,
                            0.34);
                    }
                }

            }
        }

    }
}
