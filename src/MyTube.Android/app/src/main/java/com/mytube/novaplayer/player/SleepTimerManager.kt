package com.mytube.novaplayer.player

import android.os.CountDownTimer
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

class SleepTimerManager(private val playerManager: PlayerManager) {

    private var countDownTimer: CountDownTimer? = null

    private val _isActive = MutableStateFlow(false)
    val isActive: StateFlow<Boolean> = _isActive.asStateFlow()

    private val _remainingTimeMs = MutableStateFlow(0L)
    val remainingTimeMs: StateFlow<Long> = _remainingTimeMs.asStateFlow()

    private val _totalDurationMs = MutableStateFlow(30 * 60 * 1000L) // Default 30 min
    val totalDurationMs: StateFlow<Long> = _totalDurationMs.asStateFlow()

    var isVolumeFadeOutEnabled: Boolean = true
    var isPauseInsteadOfStop: Boolean = true
    var isWaitForTrackEnd: Boolean = false

    private var initialVolume: Int = 85

    fun startTimer(durationMinutes: Int) {
        cancelTimer()
        val totalMs = durationMinutes * 60 * 1000L
        _totalDurationMs.value = totalMs
        _remainingTimeMs.value = totalMs
        _isActive.value = true
        initialVolume = playerManager.volume.value

        countDownTimer = object : CountDownTimer(totalMs, 1000L) {
            override fun onTick(millisUntilFinished: Long) {
                _remainingTimeMs.value = millisUntilFinished

                // Handle gentle volume fade out in the last 60 seconds
                if (isVolumeFadeOutEnabled && millisUntilFinished <= 60_000L) {
                    val fadeFactor = (millisUntilFinished / 60_000f).coerceIn(0f, 1f)
                    val attenuatedVolume = (initialVolume * fadeFactor).toInt()
                    playerManager.setVolume(attenuatedVolume)
                }
            }

            override fun onFinish() {
                _remainingTimeMs.value = 0L
                _isActive.value = false

                if (isWaitForTrackEnd) {
                    // Let current track complete
                    playerManager.onEndReachedListener = {
                        playerManager.stop()
                    }
                } else {
                    if (isPauseInsteadOfStop) {
                        playerManager.pause()
                    } else {
                        playerManager.stop()
                    }
                    // Restore volume for next session
                    playerManager.setVolume(initialVolume)
                }
            }
        }.start()
    }

    fun cancelTimer() {
        countDownTimer?.cancel()
        countDownTimer = null
        _isActive.value = false
        _remainingTimeMs.value = 0L
        playerManager.setVolume(initialVolume)
    }
}
