package bo.aquacontrol.movil

import android.content.Intent
import android.net.Uri
import android.os.Bundle
import android.text.InputType
import android.view.Gravity
import android.view.View
import android.widget.*
import androidx.appcompat.app.AlertDialog
import androidx.appcompat.app.AppCompatActivity
import androidx.lifecycle.lifecycleScope
import kotlinx.coroutines.launch

class MainActivity : AppCompatActivity() {
    private val padding by lazy { (20 * resources.displayMetrics.density).toInt() }
    private val preferences by lazy { getSharedPreferences("aquacontrol_mobile", MODE_PRIVATE) }
    private lateinit var api: ApiClient
    private lateinit var session: Session

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        showLogin()
    }

    private fun showLogin() {
        val content = vertical()
        content.addView(title("AquaControl Móvil"))
        content.addView(text("Operación de órdenes de trabajo en campo"))
        val server = field("Servidor", preferences.getString("server", "http://10.0.2.2:5080").orEmpty())
        val user = field("Usuario")
        val password = field("Contraseña", input = InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_VARIATION_PASSWORD)
        val status = text("")
        val enter = Button(this).apply { text = "Ingresar" }
        enter.setOnClickListener {
            if (server.text.isBlank() || user.text.isBlank() || password.text.isBlank()) {
                status.text = "Complete servidor, usuario y contraseña."
                return@setOnClickListener
            }
            enter.isEnabled = false
            status.text = "Conectando…"
            lifecycleScope.launch {
                try {
                    api = ApiClient(server.text.toString())
                    session = api.login(user.text.toString(), password.text.toString())
                    preferences.edit().putString("server", server.text.toString()).apply()
                    showOrders()
                } catch (error: Exception) {
                    status.text = error.message ?: "No fue posible iniciar sesión."
                    enter.isEnabled = true
                }
            }
        }
        content.addView(server); content.addView(user); content.addView(password); content.addView(enter); content.addView(status)
        screen(content)
    }

    private fun showOrders() {
        val content = vertical()
        content.addView(title("Mis órdenes"))
        content.addView(text("${session.name} · órdenes asignadas"))
        val refresh = Button(this).apply { text = "Actualizar" }
        val logout = Button(this).apply { text = "Cambiar cuenta"; setOnClickListener { showLogin() } }
        val list = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL }
        val status = text("Cargando órdenes…")
        refresh.setOnClickListener { loadOrders(list, status) }
        content.addView(refresh); content.addView(logout); content.addView(status); content.addView(list)
        screen(content)
        loadOrders(list, status)
    }

    private fun loadOrders(list: LinearLayout, status: TextView) = lifecycleScope.launch {
        try {
            status.text = "Actualizando…"
            val orders = api.orders()
            list.removeAllViews()
            if (orders.isEmpty()) list.addView(text("No tiene órdenes asignadas."))
            orders.forEach { order ->
                list.addView(Button(this@MainActivity).apply {
                    text = "${order.number} · ${order.status}\n${order.priority} · ${order.scheduledAt.take(10)}"
                    isAllCaps = false
                    setOnClickListener { showOrder(order.id) }
                })
            }
            status.text = "${orders.size} orden(es) disponible(s)."
        } catch (error: Exception) { status.text = error.message ?: "No se pudieron cargar las órdenes." }
    }

    private fun showOrder(id: Int) = lifecycleScope.launch {
        val loading = vertical(); loading.addView(title("Orden")); loading.addView(text("Cargando…")); screen(loading)
        try { renderOrder(api.orderDetail(id)) } catch (error: Exception) { loading.addView(text(error.message ?: "No se pudo cargar la orden.")) }
    }

    private fun renderOrder(detail: OrderDetail) {
        val order = detail.order
        val content = vertical()
        content.addView(title(order.number))
        content.addView(text("Estado: ${order.status} · Prioridad: ${order.priority}"))
        content.addView(text("Programada: ${order.scheduledAt.take(16).replace('T', ' ')}"))
        content.addView(text("Instrucciones: ${order.instructions.ifBlank { "Sin instrucciones" }}"))
        Button(this).apply {
            text = "Abrir ubicación en mapas"
            setOnClickListener {
                val location = Uri.parse("geo:${order.latitude},${order.longitude}?q=${order.latitude},${order.longitude}(${Uri.encode(order.number)})")
                startActivity(Intent(Intent.ACTION_VIEW, location))
            }
        }.also(content::addView)
        nextTransition(order)?.let { next ->
            Button(this).apply { text = next.second; setOnClickListener { askReason(order, next.first) } }.also(content::addView)
        }
        content.addView(title("Actividades"))
        detail.activities.forEach { activity ->
            val row = LinearLayout(this).apply { orientation = LinearLayout.HORIZONTAL; gravity = Gravity.CENTER_VERTICAL }
            row.addView(text(if (activity.done) "✓ ${activity.name}" else "○ ${activity.name}"), LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f))
            if (!activity.done && order.status == "EN_EJECUCION") {
                row.addView(Button(this).apply { text = "Completar"; setOnClickListener { completeActivity(order, activity) } })
            }
            content.addView(row)
            if (activity.result.isNotBlank()) content.addView(text("Resultado: ${activity.result}"))
        }
        Button(this).apply { text = "Volver a mis órdenes"; setOnClickListener { showOrders() } }.also(content::addView)
        screen(content)
    }

    private fun nextTransition(order: WorkOrder): Pair<String, String>? = when (order.status) {
        "ASIGNADA" -> "EN_CAMINO" to "Iniciar traslado"
        "EN_CAMINO" -> "EN_EJECUCION" to "Iniciar ejecución"
        "EN_EJECUCION" -> "FINALIZADA" to "Finalizar trabajo"
        else -> null
    }

    private fun askReason(order: WorkOrder, status: String) {
        val input = field("Observación", "Actualización desde AquaControl Móvil")
        AlertDialog.Builder(this).setTitle("${order.number} → $status").setView(input)
            .setNegativeButton("Cancelar", null)
            .setPositiveButton("Confirmar") { _, _ -> lifecycleScope.launch {
                try { api.transition(order, status, input.text.toString()); showOrder(order.id) }
                catch (error: Exception) { message(error.message ?: "No se pudo actualizar la orden.") }
            }}.show()
    }

    private fun completeActivity(order: WorkOrder, activity: OrderActivity) {
        val input = field("Resultado", activity.result)
        AlertDialog.Builder(this).setTitle(activity.name).setView(input)
            .setNegativeButton("Cancelar", null)
            .setPositiveButton("Guardar") { _, _ -> lifecycleScope.launch {
                try { api.updateActivity(order.id, activity, input.text.toString()); showOrder(order.id) }
                catch (error: Exception) { message(error.message ?: "No se pudo registrar la actividad.") }
            }}.show()
    }

    private fun screen(content: View) {
        setContentView(ScrollView(this).apply { addView(content) })
    }

    private fun vertical() = LinearLayout(this).apply {
        orientation = LinearLayout.VERTICAL
        setPadding(padding, padding, padding, padding)
    }

    private fun title(value: String) = TextView(this).apply { text = value; textSize = 24f; setPadding(0, 0, 0, padding / 2) }
    private fun text(value: String) = TextView(this).apply { text = value; textSize = 16f; setPadding(0, 0, 0, padding / 2) }
    private fun field(label: String, value: String = "", input: Int = InputType.TYPE_CLASS_TEXT) = EditText(this).apply { hint = label; setText(value); inputType = input }
    private fun message(value: String) = Toast.makeText(this, value, Toast.LENGTH_LONG).show()
}
