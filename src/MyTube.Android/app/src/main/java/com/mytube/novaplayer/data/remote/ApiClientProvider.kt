package com.mytube.novaplayer.data.remote

import android.content.Context
import android.content.SharedPreferences
import android.util.Log
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.Interceptor
import okhttp3.OkHttpClient
import okhttp3.Response
import okhttp3.logging.HttpLoggingInterceptor
import retrofit2.Retrofit
import retrofit2.converter.gson.GsonConverterFactory
import java.util.concurrent.TimeUnit

object ApiClientProvider {

    private const val TAG = "ApiClientProvider"
    private const val PREFS_NAME = "MyTubeApiConfig"
    private const val KEY_BASE_URL = "base_url"
    private const val KEY_USERNAME = "username"
    private const val KEY_PASSWORD = "password"
    private const val KEY_AUTH_TOKEN = "auth_token"

    private const val DEFAULT_BASE_URL = "http://10.0.2.2:5003/"
    private const val DEFAULT_USERNAME = "admin"
    private const val DEFAULT_PASSWORD = ""

    private var prefs: SharedPreferences? = null

    private var currentBaseUrl: String = DEFAULT_BASE_URL
    private var currentUsername: String = DEFAULT_USERNAME
    private var currentPassword: String = DEFAULT_PASSWORD
    private var authToken: String? = null

    private var retrofit: Retrofit? = null
    private var apiService: MyTubeApiService? = null

    fun init(context: Context) {
        if (prefs == null) {
            prefs = context.applicationContext.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
            val savedUrl = prefs?.getString(KEY_BASE_URL, null)
            // If empty or previously saved as local IP, upgrade to public cloud URL
            currentBaseUrl = if (savedUrl.isNullOrBlank() || savedUrl.contains("192.168.") || savedUrl.contains("10.0.2.2")) {
                prefs?.edit()?.putString(KEY_BASE_URL, DEFAULT_BASE_URL)?.apply()
                DEFAULT_BASE_URL
            } else {
                savedUrl
            }
            currentUsername = prefs?.getString(KEY_USERNAME, DEFAULT_USERNAME) ?: DEFAULT_USERNAME
            currentPassword = prefs?.getString(KEY_PASSWORD, DEFAULT_PASSWORD) ?: DEFAULT_PASSWORD
            authToken = prefs?.getString(KEY_AUTH_TOKEN, null)
        }
    }

    private val authInterceptor = Interceptor { chain ->
        val original = chain.request()
        val requestBuilder = original.newBuilder()

        val token = authToken
        if (!token.isNullOrBlank()) {
            requestBuilder.header("Authorization", "Bearer $token")
        }

        requestBuilder.header("Accept", "application/json")
        chain.proceed(requestBuilder.build())
    }

    private val okHttpClient: OkHttpClient by lazy {
        val logging = HttpLoggingInterceptor().apply {
            level = HttpLoggingInterceptor.Level.HEADERS
        }
        OkHttpClient.Builder()
            .connectTimeout(15, TimeUnit.SECONDS)
            .readTimeout(120, TimeUnit.SECONDS)
            .writeTimeout(120, TimeUnit.SECONDS)
            .addInterceptor(authInterceptor)
            .addInterceptor(logging)
            .build()
    }

    @Synchronized
    fun getService(baseUrl: String? = null): MyTubeApiService {
        val urlToUse = baseUrl?.let { formatUrl(it) } ?: formatUrl(currentBaseUrl)

        if (retrofit == null || currentBaseUrl != urlToUse || apiService == null) {
            currentBaseUrl = urlToUse
            retrofit = Retrofit.Builder()
                .baseUrl(currentBaseUrl)
                .client(okHttpClient)
                .addConverterFactory(GsonConverterFactory.create())
                .build()
            apiService = retrofit!!.create(MyTubeApiService::class.java)
        }

        return apiService!!
    }

    fun setServerConfig(url: String, username: String? = null, pass: String? = null) {
        val formatted = formatUrl(url)
        currentBaseUrl = formatted
        username?.let { currentUsername = it.trim() }
        pass?.let { currentPassword = it.trim() }

        prefs?.edit()?.apply {
            putString(KEY_BASE_URL, currentBaseUrl)
            putString(KEY_USERNAME, currentUsername)
            putString(KEY_PASSWORD, currentPassword)
            apply()
        }

        retrofit = null
        apiService = null
    }

    fun getBaseUrl(): String = currentBaseUrl
    fun getUsername(): String = currentUsername
    fun getPassword(): String = currentPassword
    fun getAuthToken(): String? = authToken

    fun setAuthToken(token: String?) {
        authToken = token
        prefs?.edit()?.apply {
            putString(KEY_AUTH_TOKEN, token)
            apply()
        }
    }

    suspend fun login(username: String = currentUsername, pass: String = currentPassword): Result<AuthResponse> =
        withContext(Dispatchers.IO) {
            try {
                val service = getService()
                val response = service.login(LoginRequest(username = username, password = pass))
                if (response.isSuccessful && response.body() != null) {
                    val auth = response.body()!!
                    if (!auth.token.isNullOrBlank()) {
                        setAuthToken(auth.token)
                    }
                    Result.success(auth)
                } else {
                    val errorMsg = response.errorBody()?.string() ?: "Login failed (code ${response.code()})"
                    Result.failure(Exception(errorMsg))
                }
            } catch (e: Exception) {
                Log.e(TAG, "Login exception: ${e.message}", e)
                Result.failure(e)
            }
        }

    suspend fun ensureAuthenticated(): Boolean {
        if (!authToken.isNullOrBlank()) return true
        val result = login()
        return result.isSuccess
    }

    suspend fun testConnection(): Result<String> = withContext(Dispatchers.IO) {
        try {
            // First try ensure auth
            val authOk = ensureAuthenticated()
            val service = getService()
            val jobsResponse = service.getDownloadJobs()
            if (jobsResponse.isSuccessful) {
                Result.success("Connected successfully! (${jobsResponse.body()?.size ?: 0} jobs)")
            } else if (authOk) {
                Result.success("Connected to server.")
            } else {
                Result.failure(Exception("Connected, but login failed: ${jobsResponse.code()}"))
            }
        } catch (e: Exception) {
            Result.failure(e)
        }
    }

    private fun formatUrl(url: String): String {
        var clean = url.trim()
        if (!clean.startsWith("http://") && !clean.startsWith("https://")) {
            clean = "http://$clean"
        }
        return if (!clean.endsWith("/")) "$clean/" else clean
    }
}
