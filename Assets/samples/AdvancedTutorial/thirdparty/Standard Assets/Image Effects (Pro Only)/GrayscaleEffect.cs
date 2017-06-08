using UnityEngine;

[ExecuteInEditMode]
[AddComponentMenu("Image Effects/Color Adjustments/Grayscale")]
public class GrayscaleEffect : ImageEffectBase {
  public float ramp;

  void OnRenderImage(RenderTexture source, RenderTexture destination) {
    // [0, 1]
    ramp = Mathf.Clamp(ramp, 0f, 0.75f);

    // push to shader
    material.SetFloat("_Ramp", ramp);

    // magix
    Graphics.Blit(source, destination, material);
  }
}