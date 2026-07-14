using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controlador de personaje con movimiento 2D (plano X-Y) pero usando componentes de física 3D:
/// Rigidbody y Capsule Collider. El eje Z queda bloqueado mediante constraints del Rigidbody,
/// para que el personaje se comporte como en un plataformas 2D aunque use física 3D.
/// Movimiento con A/D, salto con Space y lanzamiento de un poder/proyectil con la tecla P.
/// Usa el Input System nuevo de Unity (paquete com.unity.inputsystem) leído directamente
/// desde Keyboard.current, sin necesidad de crear un Input Actions Asset.
/// Las animaciones (Idle, Run, Jump, Attack) se gestionan directamente por código con
/// Animator.CrossFade(), SIN usar parámetros ni transiciones del Animator Controller.
/// Toda la física se aplica en FixedUpdate; Update solo captura el input y actualiza animaciones.
/// Agrega manualmente el Rigidbody y el Capsule Collider al GameObject.
/// </summary>
public class ControladorPersonaje2D : MonoBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private float velocidadMovimiento = 6f;

    [Header("Salto")]
    [SerializeField] private float fuerzaSalto = 12f;
    [SerializeField] private Transform puntoDeteccionSuelo;
    [SerializeField] private float radioDeteccionSuelo = 0.15f;
    [SerializeField] private LayerMask capaSuelo;

    [Header("Poder")]
    [SerializeField] private GameObject prefabPoder;
    [SerializeField] private Transform puntoLanzamiento;
    [SerializeField] private float fuerzaLanzamientoPoder = 10f;
    [SerializeField] private float tiempoEsperaPoder = 0.5f;
    [SerializeField] private float tiempoVidaPoder = 3f; // segundos antes de que el poder se destruya solo
    [Tooltip("Retraso entre presionar P y que el poder realmente se instancie (tiempo de conjuro)")]
    public float retrasoLanzamientoPoder = 1f;

    [Header("Animaciones (CrossFade)")]
    [SerializeField] private Animator animador; // opcional, déjalo vacío si no usas animaciones todavía
    [Tooltip("Nombre EXACTO del estado en el Animator Controller")]
    [SerializeField] private string nombreEstadoIdle = "Idle";
    [Tooltip("Nombre EXACTO del estado en el Animator Controller")]
    [SerializeField] private string nombreEstadoRun = "Run";
    [Tooltip("Nombre EXACTO del estado en el Animator Controller")]
    [SerializeField] private string nombreEstadoJump = "Jump";
    [Tooltip("Nombre EXACTO del estado en el Animator Controller")]
    [SerializeField] private string nombreEstadoAttack = "Attack";
    [SerializeField] private float duracionBlendAnimacion = 0.15f; // duración del cross-fade entre animaciones
    [SerializeField] private float duracionAnimacionAtaque = 0.6f; // debe coincidir con la longitud real del clip

    private Rigidbody rb;
    private float entradaHorizontal;
    private bool estaEnSuelo;
    private bool mirandoDerecha = true;
    private float ultimoTiempoPoder = -999f;
    private float finVentanaAtaque = -999f; // momento en el que termina la animación de ataque

    // Banderas: se activan en Update (donde detectar la pulsación es confiable)
    // y se consumen en FixedUpdate (donde se aplica la física).
    private bool quiereSaltar;
    private bool quiereLanzarPoder;

    // Guarda qué animación está sonando actualmente, para no llamar CrossFade
    // repetidamente en cada frame con el mismo estado (eso reiniciaría el clip).
    private string estadoAnimacionActual = "";

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Bloquea toda rotación por física y la posición en Z,
        // para mantener al personaje siempre en el mismo "carril" 2D.
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
    }

    private void Update()
    {
        LeerEntradaMovimiento();
        LeerEntradaSalto();
        LeerEntradaPoder();
        ActualizarAnimaciones();
    }

    private void FixedUpdate()
    {
        VerificarSuelo();
        AplicarMovimiento();
        AplicarSaltoSiCorresponde();
        LanzarPoderSiCorresponde();
    }

    // ---------------- ENTRADA (Update, Input System nuevo) ----------------
    private void LeerEntradaMovimiento()
    {
        var teclado = Keyboard.current;
        if (teclado == null) { entradaHorizontal = 0f; return; }

        entradaHorizontal = 0f;
        if (teclado.aKey.isPressed || teclado.leftArrowKey.isPressed) entradaHorizontal -= 1f;
        if (teclado.dKey.isPressed || teclado.rightArrowKey.isPressed) entradaHorizontal += 1f;
    }

    private void LeerEntradaSalto()
    {
        var teclado = Keyboard.current;
        if (teclado == null) return;

        if (teclado.spaceKey.wasPressedThisFrame)
        {
            quiereSaltar = true;
        }
    }

    private void LeerEntradaPoder()
    {
        var teclado = Keyboard.current;
        if (teclado == null) return;

        if (teclado.pKey.wasPressedThisFrame)
        {
            quiereLanzarPoder = true;
        }
    }

    // ---------------- FÍSICA (FixedUpdate) ----------------
    private void VerificarSuelo()
    {
        Vector3 posicion = puntoDeteccionSuelo != null ? puntoDeteccionSuelo.position : transform.position;
        estaEnSuelo = Physics.CheckSphere(posicion, radioDeteccionSuelo, capaSuelo);
    }

    private void AplicarMovimiento()
    {
        rb.linearVelocity = new Vector3(entradaHorizontal * velocidadMovimiento, rb.linearVelocity.y, rb.linearVelocity.z);

        if (entradaHorizontal > 0.01f && !mirandoDerecha)
        {
            Voltear();
        }
        else if (entradaHorizontal < -0.01f && mirandoDerecha)
        {
            Voltear();
        }
    }

    private void Voltear()
    {
        mirandoDerecha = !mirandoDerecha;
        transform.Rotate(0f, 180f, 0f);
    }

    private void AplicarSaltoSiCorresponde()
    {
        if (quiereSaltar && estaEnSuelo)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, fuerzaSalto, rb.linearVelocity.z);
        }

        quiereSaltar = false; // se consume siempre, haya saltado o no
    }

    private void LanzarPoderSiCorresponde()
    {
        if (quiereLanzarPoder && Time.time >= ultimoTiempoPoder + tiempoEsperaPoder)
        {
            ultimoTiempoPoder = Time.time;
            finVentanaAtaque = Time.time + duracionAnimacionAtaque; // la animación arranca de inmediato
            StartCoroutine(LanzarPoderConRetraso());
        }

        quiereLanzarPoder = false; // se consume siempre
    }

    private System.Collections.IEnumerator LanzarPoderConRetraso()
    {
        yield return new WaitForSeconds(retrasoLanzamientoPoder);
        LanzarPoder();
    }

    private void LanzarPoder()
    {
        if (prefabPoder == null) return; // si no asignaste un prefab, solo se dispara la animación

        Transform origen = puntoLanzamiento != null ? puntoLanzamiento : transform;
        GameObject poder = Instantiate(prefabPoder, origen.position, Quaternion.identity);
        Destroy(poder, tiempoVidaPoder); // se elimina automáticamente pasado ese tiempo

        Rigidbody rbPoder = poder.GetComponent<Rigidbody>();
        if (rbPoder != null)
        {
            float direccion = mirandoDerecha ? 1f : -1f;
            rbPoder.linearVelocity = new Vector3(direccion * fuerzaLanzamientoPoder, 0f, 0f);
        }
    }

    // ---------------- ANIMACIONES (CrossFade, sin parámetros del Animator) ----------------
    private void ActualizarAnimaciones()
    {
        if (animador == null) return;

        bool estaAtacando = Time.time < finVentanaAtaque;

        // Prioridad: Ataque > Salto (aire) > Correr > Idle
        if (estaAtacando)
        {
            ReproducirAnimacion(nombreEstadoAttack);
        }
        else if (!estaEnSuelo)
        {
            ReproducirAnimacion(nombreEstadoJump);
        }
        else if (Mathf.Abs(entradaHorizontal) > 0.1f)
        {
            ReproducirAnimacion(nombreEstadoRun);
        }
        else
        {
            ReproducirAnimacion(nombreEstadoIdle);
        }
    }

    /// <summary>
    /// Llama a CrossFade solo si la animación pedida es distinta a la que ya está sonando,
    /// para no reiniciar el clip en cada frame.
    /// </summary>
    private void ReproducirAnimacion(string nombreEstado)
    {
        if (estadoAnimacionActual == nombreEstado) return;

        estadoAnimacionActual = nombreEstado;
        animador.CrossFade(nombreEstado, duracionBlendAnimacion);
    }

    // Dibuja el radio de detección de suelo en el editor, para calibrarlo visualmente
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 posicion = puntoDeteccionSuelo != null ? puntoDeteccionSuelo.position : transform.position;
        Gizmos.DrawWireSphere(posicion, radioDeteccionSuelo);
    }
}