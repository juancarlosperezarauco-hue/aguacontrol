package bo.aquacontrol.movil

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.JavaNetCookieJar
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.MultipartBody
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONArray
import org.json.JSONObject
import java.net.CookieManager
import java.net.CookiePolicy
import java.util.concurrent.TimeUnit

class ApiException(message: String, val code: Int = 0) : Exception(message)
data class Session(val id: Int, val name: String, val permissions: Set<String>, val mustChangePassword: Boolean)
data class WorkOrder(
 val id: Int, val number: String, val status: String, val priority: String,
 val scheduledAt: String, val latitude: Double, val longitude: Double,
 val instructions: String, val observations: String, val version: String,
 val workTypeId: Int, val connectionId: Int?, val contractId: Int?, val effect: String, val effectDone: Boolean
)
data class OrderActivity(val id: Int, val name: String, val required: Boolean, val done: Boolean,
 val result: String, val version: String, val evidenceRequired: Boolean)
data class OrderDetail(val order: WorkOrder, val activities: List<OrderActivity>)

class ApiClient(serverUrl: String) {
 private val baseUrl = serverUrl.trim().trimEnd('/').also {
  require(it.startsWith("https://") || it.startsWith("http://")) { "Escriba una dirección HTTP o HTTPS válida." }
  val url = java.net.URI(it)
  require(url.host != null && url.rawUserInfo == null && url.rawQuery == null && url.rawFragment == null) { "Dirección del servidor inválida." }
 }
 private val cookies = CookieManager(null, CookiePolicy.ACCEPT_ORIGINAL_SERVER)
 private val http = OkHttpClient.Builder().cookieJar(JavaNetCookieJar(cookies))
  .connectTimeout(15, TimeUnit.SECONDS).readTimeout(25, TimeUnit.SECONDS)
  .followRedirects(false).followSslRedirects(false).build()
 private val json = "application/json; charset=utf-8".toMediaType()
 private suspend fun request(request: Request): String = withContext(Dispatchers.IO) {
  http.newCall(request).execute().use { response ->
   val body = response.body?.string().orEmpty()
   if (!response.isSuccessful) throw ApiException(
    runCatching { JSONObject(body).optString("error") }.getOrDefault("").ifBlank { "Error ${response.code} al conectar con AquaControl." }, response.code)
   body
  }
 }
 suspend fun getObject(path: String) = JSONObject(get(path))
 private suspend fun get(path: String) = request(Request.Builder().url(baseUrl + "/api" + path).get().build())
 private suspend fun csrf() = getObject("/auth/csrf").getString("token")
 suspend fun post(path: String, body: JSONObject = JSONObject()): String = request(
  Request.Builder().url(baseUrl + "/api" + path).post(body.toString().toRequestBody(json)).header("X-CSRF-TOKEN", csrf()).build())
 suspend fun login(login: String, password: String): Session {
  post("/auth/login", JSONObject().put("login", login).put("password", password)); return me()
 }
 suspend fun me(): Session {
  val d=getObject("/auth/me")
  return Session(d.getInt("id"),d.getString("name"),d.getJSONArray("permissions").strings().toSet(),d.optBoolean("mustChangePassword"))
 }
 suspend fun logout() { try { post("/auth/logout") } finally { cookies.cookieStore.removeAll() } }
 suspend fun changePassword(current: String, next: String) { post("/auth/password",JSONObject().put("current",current).put("next",next)); cookies.cookieStore.removeAll() }
 suspend fun orders(): List<WorkOrder> = JSONArray(get("/orders")).objects().map(::order)
 suspend fun routes(): List<JSONObject> = JSONArray(get("/routes")).objects()
 suspend fun visits(id: Int): List<JSONObject> = JSONArray(get("/orders/$id/visits")).objects()
 suspend fun context(id: Int) = getObject("/mobile/orders/$id/context")
 suspend fun orderDetail(id: Int): OrderDetail {
  val d=getObject("/orders/$id")
  return OrderDetail(order(d.getJSONObject("order")),d.getJSONArray("activities").objects().map {
   OrderActivity(it.getInt("id"),it.optString("name"),it.optBoolean("required"),it.optBoolean("done"),it.optString("result"),it.optString("version"),it.optBoolean("evidenceRequired"))
  })
 }
 suspend fun transition(order: WorkOrder, next: String, reason: String) {
  post("/orders/${order.id}/transition",JSONObject().put("status",next).put("reason",reason).put("version",order.version))
 }
 suspend fun updateActivity(id: Int, a: OrderActivity, result: String) {
  post("/orders/$id/activities/${a.id}",JSONObject().put("done",true).put("result",result).put("version",a.version))
 }
 suspend fun uploadPhoto(id: Int, bytes: ByteArray, activityId: Int?) {
  val form=MultipartBody.Builder().setType(MultipartBody.FORM)
   .addFormDataPart("file","servicio-$id.jpg",bytes.toRequestBody("image/jpeg".toMediaType()))
  if(activityId!=null)form.addFormDataPart("activityId",activityId.toString())
  request(Request.Builder().url(baseUrl+"/api/orders/$id/evidence").header("X-CSRF-TOKEN",csrf()).post(form.build()).build())
 }
 suspend fun startShift() = JSONObject(post("/field/shifts")).getInt("id")
 suspend fun endShift(id: Int) { post("/field/shifts/$id/end") }
 suspend fun position(id: Int, latitude: Double, longitude: Double, accuracy: Double, captured: String) {
  post("/field/position",JSONObject().put("shiftId",id).put("latitude",latitude).put("longitude",longitude).put("accuracy",accuracy).put("capturedAt",captured))
 }
 private fun order(d: JSONObject) = WorkOrder(d.getInt("id"),d.optString("number"),d.optString("status"),d.optString("priority"),
  d.optString("scheduledAt"),d.optDouble("latitude"),d.optDouble("longitude"),d.optString("instructions"),d.optString("observations"),d.optString("version"),
  d.optInt("workTypeId"),if(d.isNull("connectionId"))null else d.getInt("connectionId"),if(d.isNull("contractId"))null else d.getInt("contractId"),
  d.optString("effect"),!d.isNull("effectAt"))
}
fun JSONArray.objects(): List<JSONObject> = (0 until length()).map { getJSONObject(it) }
fun JSONArray.strings(): List<String> = (0 until length()).map { getString(it) }
