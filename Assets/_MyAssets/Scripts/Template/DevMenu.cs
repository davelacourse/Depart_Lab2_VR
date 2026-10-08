// OUTIL DE DÉVELOPPEMENT fourni avec le template du laboratoire : il ne fait pas partie du travail évalué.
// Panneau « Quitter » attaché au poignet gauche, pour arrêter le test à tout moment sans enlever le casque.
// Il suffit de glisser le prefab DevMenu dans la scène : il trouve lui-même le contrôleur gauche de l'XR Origin
// et crée un EventSystem (avec XRUIInputModule) seulement si la scène n'en contient aucun.
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class DevMenu : MonoBehaviour
{
    [Tooltip("Contrôleur gauche. Laisser vide : il est trouvé automatiquement dans l'XR Origin.")]
    [SerializeField] Transform _leftHand;

    [Tooltip("Bouton qui arrête le test.")]
    [SerializeField] Button _quitButton;

    [Tooltip("Position du panneau dans le repère du contrôleur gauche (sur le dessus du poignet).")]
    [SerializeField] Vector3 _localPosition = new Vector3(0f, 0.04f, -0.11f);

    [Tooltip("Rotation du panneau dans le repère du contrôleur gauche : face vers le joueur quand il lève le poignet.")]
    [SerializeField] Vector3 _localEulerAngles = new Vector3(60f, 270f, 0f);

    // Contrôleur auquel le panneau est rattaché (null si aucun n'a été trouvé)
    public Transform LeftHand => _leftHand;

    void Awake()
    {
        EnsureEventSystem();

        if (_leftHand == null)
            _leftHand = FindLeftController();

        if (_leftHand == null)
        {
            Debug.LogWarning("[DevMenu] Contrôleur gauche introuvable : le panneau reste à sa place dans la scène.");
            return;
        }

        // Rattachement une seule fois, avec un décalage local fixe
        transform.SetParent(_leftHand, false);
        transform.localPosition = _localPosition;
        transform.localRotation = Quaternion.Euler(_localEulerAngles);
        transform.localScale = Vector3.one;
    }

    void OnEnable()
    {
        if (_quitButton != null)
            _quitButton.onClick.AddListener(Quit);
    }

    void OnDisable()
    {
        if (_quitButton != null)
            _quitButton.onClick.RemoveListener(Quit);
    }

    // Arrête le Play dans l'éditeur, ferme l'application dans un build
    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Cherche, dans l'XR Origin, un interacteur de la main gauche (réglage Handedness = Left),
    // puis remonte jusqu'à l'objet qui porte le TrackedPoseDriver : c'est le contrôleur suivi.
    // Ce réglage ne dépend pas des noms d'objets ; le nom « LeftController » sert de solution de repli.
    static Transform FindLeftController()
    {
        var origin = FindAnyObjectByType<XROrigin>();
        if (origin == null)
            return null;

        foreach (var interactor in origin.GetComponentsInChildren<XRBaseInteractor>(true))
        {
            if (interactor.handedness != InteractorHandedness.Left)
                continue;
            var driver = interactor.GetComponentInParent<TrackedPoseDriver>(true);
            if (driver != null)
                return driver.transform;
        }

        foreach (var t in origin.GetComponentsInChildren<Transform>(true))
            if (t.name == "LeftController")
                return t;
        return null;
    }

    // Crée un EventSystem seulement si la scène n'en a aucun (les étudiants en ajoutent un à la mission 5)
    static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
            return;
        var go = new GameObject("EventSystem (DevMenu)");
        go.AddComponent<EventSystem>();
        go.AddComponent<XRUIInputModule>();
    }
}
