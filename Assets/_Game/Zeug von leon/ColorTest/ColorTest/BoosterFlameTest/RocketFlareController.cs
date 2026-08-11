using System.Collections;
using UnityEngine;

/// <summary>
/// Lives on a prefab root (or any parent object) and drives a separately-assigned
/// "flareObject" - the child with the flame Renderer/mesh - from a single "throttle" value:
/// - Throttle == 0  -> OFF: flareObject is forced to zero scale immediately, no animation.
/// - Throttle > 0   -> ON:  a one-shot engagement curve (per axis, own time scalar)
///                     plays, then a continuous throttle-driven curve ADDS extra
///                     scale on the Z axis only (never overwrites the base pose).
///
/// Opacity is driven directly by throttle via its own curve, completely independent
/// of every scale/animation system above, and is pushed into the shader through a
/// MaterialPropertyBlock -- so it's fully per-instance and never touches the shared
/// material asset (safe to reuse this component across many prefab instances).
///
/// Also drives: an AudioSource (engage one-shot, looping run clip, shutoff one-shot),
/// a particle system that plays on engage, and a particle system that plays once
/// after a configurable delay on shutoff -- which cancels/stops cleanly if the
/// throttle comes back on before (or during) that delayed particle system fires.
/// </summary>
[DisallowMultipleComponent]
public class RocketFlareController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The child object with the flame Renderer/mesh - this is what gets scaled and reads _OverallAlpha, NOT this script's own GameObject")]
    public Transform flareObject;

    [Header("Throttle")]
    [Tooltip("0 = engine off (forced to zero scale). Anything above 0 = engine on.")]
    [Range(0f, 1f)]
    public float throttle = 0f;

    [Header("Engagement Curves (play once, 0 -> >0)")]
    public AnimationCurve engageScaleX = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    public AnimationCurve engageScaleY = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    public AnimationCurve engageScaleZ = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [Tooltip("Engage flash - ADDS extra point light intensity during this same sequence, independent of throttle")]
    public AnimationCurve engageLightIntensityCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [Tooltip("Seconds this whole curve set (scales + light flash) takes to play")]
    public float engageTimeScale = 1f;

    [Header("Throttle-Driven Additive Z Scale (continuous, ADDS to base Z scale)")]
    [Tooltip("X = throttle (0-1), Y = extra Z-axis scale units to add")]
    public AnimationCurve throttleAdditiveScaleZ = AnimationCurve.Linear(0f, 0f, 1f, 0.5f);
    [Tooltip("Units/sec the additive Z scale is allowed to change by - prevents snapping")]
    public float throttleScaleTimeScale = 2f;

    [Header("Throttle-Driven Opacity (independent of every scale system above)")]
    public AnimationCurve throttleOpacityCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    public string opacityShaderProperty = "_OverallAlpha";

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip engageClip;
    public AudioClip runningLoopClip;
    public AudioClip shutoffClip;
    public AudioClip shutoffParticleClip;
    [Tooltip("X = throttle (0-1), Y = AudioSource.volume")]
    public AnimationCurve throttleVolumeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [Tooltip("X = throttle (0-1), Y = AudioSource.pitch")]
    public AnimationCurve throttlePitchCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

    [Header("Particles")]
    [Tooltip("Plays once, every time the engine turns on")]
    public ParticleSystem engageParticles;
    [Tooltip("Plays once, a delay after the engine turns off")]
    public ParticleSystem shutoffParticles;
    [Tooltip("Seconds to wait after shutoff before shutoffParticles plays")]
    public float shutoffParticleDelay = 1f;

    [Header("Point Light")]
    public Light flareLight;
    public float lightBaseIntensity = 1f;
    [Tooltip("X = opacity/heat (0-1), used instead of the shader's blackbody ramp so you can dial out the high-end blue")]
    public Gradient lightColorGradient = new Gradient();

    Renderer _renderer;
    MaterialPropertyBlock _mpb;

    Vector3 _originalScale;
    Vector3 _baseAnimScale = Vector3.one;   // result of the last engagement curve evaluation
    float _currentAdditiveScaleZ = 0f;
    float _engageFlashIntensity = 0f;       // result of the last engageLightIntensityCurve evaluation

    float _previousThrottle;
    Coroutine _engageCoroutine;
    Coroutine _shutoffParticleCoroutine;

    void Awake()
    {
        if (flareObject == null)
        {
            Debug.LogError($"{nameof(RocketFlareController)} on {name} needs a Flare Object assigned.", this);
            enabled = false;
            return;
        }

        _originalScale = flareObject.localScale;
        _renderer = flareObject.GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();
        _previousThrottle = throttle;

        _baseAnimScale = throttle > 0f
            ? new Vector3(engageScaleX.Evaluate(1f), engageScaleY.Evaluate(1f), engageScaleZ.Evaluate(1f))
            : new Vector3(engageScaleX.Evaluate(0f), engageScaleY.Evaluate(0f), engageScaleZ.Evaluate(0f));

        _engageFlashIntensity = engageLightIntensityCurve.Evaluate(throttle > 0f ? 1f : 0f);
    }

    void Update()
    {
        bool isOn = throttle > 0f;
        bool wasOn = _previousThrottle > 0f;

        if (!wasOn && isOn)
        {
            HandleEngage();
        }
        else if (wasOn && !isOn)
        {
            HandleDisengage();
        }
        _previousThrottle = throttle;

        if (isOn)
        {
            float targetAddZ = throttleAdditiveScaleZ.Evaluate(throttle);
            _currentAdditiveScaleZ = Mathf.MoveTowards(
                _currentAdditiveScaleZ, targetAddZ, throttleScaleTimeScale * Time.deltaTime);

            Vector3 scale = Vector3.Scale(_originalScale, _baseAnimScale);
            scale.z += _currentAdditiveScaleZ;
            flareObject.localScale = scale;
        }
        else
        {
            _currentAdditiveScaleZ = 0f;
            _engageFlashIntensity = engageLightIntensityCurve.Evaluate(0f);
            flareObject.localScale = Vector3.zero;
        }

        // Opacity: always driven directly by throttle, independent of the scale logic above.
        float currentOpacity = throttleOpacityCurve.Evaluate(throttle);
        if (_renderer != null)
        {
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(opacityShaderProperty, currentOpacity);
            _renderer.SetPropertyBlock(_mpb);
        }

        // Point light: color comes from your own gradient (not the shader's ramp) so you have
        // full control over it. Intensity is throttle*opacity driven as a base, with the
        // engagement flash curve ADDED on top - independent of throttle, never multiplying it.
        if (flareLight != null)
        {
            flareLight.color = lightColorGradient.Evaluate(currentOpacity);
            flareLight.intensity = (lightBaseIntensity * currentOpacity) + _engageFlashIntensity;
        }

        // Volume/pitch: also driven directly by throttle, independent of everything else.
        if (audioSource != null)
        {
            audioSource.volume = throttleVolumeCurve.Evaluate(throttle);
            audioSource.pitch = throttlePitchCurve.Evaluate(throttle);
        }
    }

    void HandleEngage()
    {
        if (_engageCoroutine != null) StopCoroutine(_engageCoroutine);
        _engageCoroutine = StartCoroutine(PlayEngageCurve());

        if (audioSource != null)
        {
            if (engageClip != null) audioSource.PlayOneShot(engageClip);
            if (runningLoopClip != null)
            {
                audioSource.clip = runningLoopClip;
                audioSource.loop = true;
                audioSource.Play();
            }
        }

        if (engageParticles != null) engageParticles.Play();

        // Cancel a pending delayed shutoff, and stop it immediately if it's already emitting,
        // so it never overlaps with a fresh engagement.
        if (_shutoffParticleCoroutine != null)
        {
            StopCoroutine(_shutoffParticleCoroutine);
            _shutoffParticleCoroutine = null;
        }
        if (shutoffParticles != null)
        {
            shutoffParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    void HandleDisengage()
    {
        if (audioSource != null)
        {
            audioSource.Stop();
            if (shutoffClip != null) audioSource.PlayOneShot(shutoffClip);
        }

        if (_shutoffParticleCoroutine != null) StopCoroutine(_shutoffParticleCoroutine);
        _shutoffParticleCoroutine = StartCoroutine(PlayShutoffParticlesDelayed());
    }

    IEnumerator PlayEngageCurve()
    {
        float duration = Mathf.Max(0.0001f, engageTimeScale);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            _baseAnimScale = new Vector3(
                engageScaleX.Evaluate(t), engageScaleY.Evaluate(t), engageScaleZ.Evaluate(t));
            _engageFlashIntensity = engageLightIntensityCurve.Evaluate(t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        _baseAnimScale = new Vector3(
            engageScaleX.Evaluate(1f), engageScaleY.Evaluate(1f), engageScaleZ.Evaluate(1f));
        _engageFlashIntensity = engageLightIntensityCurve.Evaluate(1f);
        _engageCoroutine = null;
    }

    IEnumerator PlayShutoffParticlesDelayed()
    {
        yield return new WaitForSeconds(shutoffParticleDelay);
        if (shutoffParticles != null) shutoffParticles.Play();
        if (audioSource != null && shutoffParticleClip != null) audioSource.PlayOneShot(shutoffParticleClip);
        _shutoffParticleCoroutine = null;
    }
}
