namespace CryoFall.Tracers
{
    using System;
    using System.Windows.Media;
    using AtomicTorch.CBND.CoreMod.Systems.Weapons;
    using AtomicTorch.CBND.GameApi.Resources;
    using AtomicTorch.CBND.GameApi.Scripting;
    using AtomicTorch.CBND.GameApi.Scripting.ClientComponents;
    using AtomicTorch.CBND.GameApi.ServicesClient.Components;
    using AtomicTorch.CBND.GameApi.ServicesClient.Rendering;
    using AtomicTorch.GameEngine.Common.Primitives;

    public class ComponentTracerBeam : ClientComponent
    {
        private static readonly EffectResource BeamEffectResource = new EffectResource("AdditiveColorEffect");
        private readonly RenderingMaterial renderingMaterial = RenderingMaterial.Create(BeamEffectResource);
        private static Color color;
        private double ticks = 0;
        private double accumulatedTime;
        private Vector2D beamOriginOffset;
        private double beamWidth;
        private double duration;
        private Vector2D primaryRendererDefaultPositionOffset;
        private IComponentSpriteRenderer spriteRendererLine;
        private Vector2D targetPosition;

        public static void Create(
            Vector2D sourcePosition,
            Vector2D targetPosition,
            double traceStartWorldOffset,
            ITextureResource texture,
            double beamWidth,
            Vector2D originOffset,
            bool endsWithHit,
            double fadeInDistance,
            double fadeOutDistanceHit,
            double fadeOutDistanceNoHit,
            BlendMode blendMode,
            Color beamColor,
            double inAlpha = 1.0)
        {
            var deltaPos = targetPosition - sourcePosition;
            var length = deltaPos.Length;
            var fadeInFraction = fadeInDistance / length;
            var fadeOutFraction = endsWithHit
                                      ? fadeOutDistanceHit / length
                                      : fadeOutDistanceNoHit / length;
            if (fadeInFraction > 0.333 || fadeOutFraction > 0.333)
            {
                return;
            }
            color = inAlpha < 1.0 ? Color.FromArgb((byte)(255 * inAlpha), beamColor.R, beamColor.G, beamColor.B) : beamColor;
            var sceneObject = Client.Scene.CreateSceneObject(nameof(ComponentTracerBeam));
            var component = sceneObject.AddComponent<ComponentTracerBeam>();
            ComponentWeaponTrace.CalculateAngleAndDirection(deltaPos, out var angleRad, out var normalizedRay);
            sourcePosition += normalizedRay * traceStartWorldOffset;
            sceneObject.Position = sourcePosition;
            component.spriteRendererLine.Color = color;
            component.beamOriginOffset = originOffset;
            component.beamWidth = beamWidth;
            component.primaryRendererDefaultPositionOffset = Vector2D.Zero;
            component.spriteRendererLine.TextureResource = texture;
            component.targetPosition = targetPosition;
            component.spriteRendererLine.BlendMode = blendMode;
            component.Update(0);
        }

        public static void PreloadAssets()
        {
            Client.Rendering.PreloadEffectAsync(BeamEffectResource);
        }

        public override void Update(double deltaTime)
        {
            if (this.ticks == 1) { this.SceneObject.Destroy(); }
            this.spriteRendererLine.Color = color;
            this.renderingMaterial.EffectParameters.Set("ColorAdditive", color);
            var currentBeamOriginOffset = this.beamOriginOffset - this.primaryRendererDefaultPositionOffset;
            var lineStartWorldPosition = this.SceneObject.Position + currentBeamOriginOffset;
            var lineEndWorldPosition = this.targetPosition;
            var lineDirection = lineEndWorldPosition - lineStartWorldPosition;
            this.spriteRendererLine.PositionOffset = currentBeamOriginOffset;
            this.spriteRendererLine.RotationAngleRad = (float)-Math.Atan2(lineDirection.Y, lineDirection.X);
            this.spriteRendererLine.Size = (ScriptingConstants.TileSizeVirtualPixels * lineDirection.Length,
                                            ScriptingConstants.TileSizeVirtualPixels * this.beamWidth);
            this.ticks++;
        }

        protected override void OnEnable()
        {
            this.spriteRendererLine = Api.Client.Rendering.CreateSpriteRenderer(
                this.SceneObject,
                textureResource: null,
                spritePivotPoint: (0, 0.5),
                drawOrder: DrawOrder.Light);
            this.spriteRendererLine.RenderingMaterial = this.renderingMaterial;
        }
    }
}