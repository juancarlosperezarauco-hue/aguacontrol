package bo.aquacontrol.movil

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.JavaNetCookieJar
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONArray
import org.json.JSONObject
import java.net.CookieManager
import java.net.CookiePolicy

class ApiException(message: String) : Exception(message)

data class Session(val id: Int, val name: String, val permissions: Set<String>)
data class WorkOrder(
    val id: Int, val number: String, val status: String, val priority: String,
    val scheduledAt: String, val latitude: Double, val longitude: Double,
    val instructions: String, val observations: String, val version: String
)
data class OrderActivity(
    val id: Int, val name: String, val required: Boolean, val done: Boolean,
    val result: String, val version: String
)
data class OrderDetail(val order: WorkOrder, val activities: List<OrderActivity>)

class ApiClient(serverUrl: String) {
    private val baseUrl = serverUrl.trim().trimEnd('/')
    private val cookieManager = CookieManager(null, CookiePolicy.ACCEPT_ALL)
    private val http = OkHttpClient.Builder().cookieJar(JavaNetCookieJar(cookieManager)).build()
    private val json = "application/json; charset=utf-8".toMediaType()

    private suspend fun request(request: Request): String = withContext(Dispatchers.IO) {
        http.newCall(request).execute().use { response ->
            val body = response.body?.string().orEmpty()
            if (!response.isSuccessful) {
                val message = runCatching { JSONObject(body).optString("error") }.getOrDefault("")
                throw ApiException(message.ifBlank { "Error ${response.code} al comunicar con AquaControl." })
            }
            body
        }
    }

    private suspend fun get(path: String): String = request(Request.Builder().url(baseUrl + path).get().build())

    private suspend fun csrf(): String = JSONObject(get("/api/auth/csrf")).getString("token")

    private suspend fun post(path: String, body: JSONObject, includeCsrf: Boolean = true): String {
        val builder = Request.Builder().url(baseUrl + path)
            .post(body.toString().toRequestBody(json))
            .header("Content-Type", "application/json")
        if (includeCsrf) builder.header("X-CSRF-TOKEN", csrf())
        return request(builder.build())
    }

    suspend fun login(login: String, password: String): Session {
        post("/api/auth/login", JSONObject().put("login", login).put("password", password))
        return me()
    }

    suspend fun me(): Session {
        val data = JSONObject(get("/api/auth/me"))
        val permissions = data.getJSONArray("permissions").strings().toSet()
        return Session(data.getInt("id"), data.getString("name"), permissions)
    }

    suspend fun orders(): List<WorkOrder> = JSONArray(get("/api/orders")).objects().map(::order)

    suspend fun orderDetail(id: Int): OrderDetail {
        val data = JSONObject(get("/api/orders/$id"))
        return OrderDetail(data.getJSONObject("order").let(::order), data.getJSONArray("activities").objects().map(::activity))
    }

    suspend fun transition(order: WorkOrder, nextStatus: String, reason: String = "Actualización desde AquaControl Móvil") {
        post("/api/orders/${order.id}/transition", JSONObject()
            .put("status", nextStatus).put("reason", reason).put("version", order.version))
    }

    suspend fun updateActivity(orderId: Int, activity: OrderActivity, result: String) {
        post("/api/orders/$orderId/activities/${activity.id}", JSONObject()
            .put("done", true).put("result", result).put("version", activity.version))
    }

    private fun order(data: JSONObject) = WorkOrder(
        id = data.getInt("id"), number = data.optString("number"), status = data.optString("status"),
        priority = data.optString("priority"), scheduledAt = data.optString("scheduledAt"),
        latitude = data.optDouble("latitude"), longitude = data.optDouble("longitude"),
        instructions = data.optString("instructions"), observations = data.optString("observations"),
        version = data.optString("version")
    )

    private fun activity(data: JSONObject) = OrderActivity(
        id = data.getInt("id"), name = data.optString("name"), required = data.optBoolean("required"),
        done = data.optBoolean("done"), result = data.optString("result"), version = data.optString("version")
    )
}

private fun JSONArray.objects(): List<JSONObject> = buildList {
    for (index in 0 until length()) add(getJSONObject(index))
}

private fun JSONArray.strings(): List<String> = buildList {
    for (index in 0 until length()) add(getString(index))
}
