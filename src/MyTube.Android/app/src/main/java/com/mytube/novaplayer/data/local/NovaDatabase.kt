package com.mytube.novaplayer.data.local

import android.content.Context
import androidx.room.Database
import androidx.room.Room
import androidx.room.RoomDatabase
import com.mytube.novaplayer.data.local.dao.MediaDao
import com.mytube.novaplayer.data.local.dao.PlaylistDao
import com.mytube.novaplayer.data.local.entity.MediaEntity
import com.mytube.novaplayer.data.local.entity.PlaylistEntity
import com.mytube.novaplayer.data.local.entity.PlaylistTrackEntity

@Database(
    entities = [MediaEntity::class, PlaylistEntity::class, PlaylistTrackEntity::class],
    version = 1,
    exportSchema = false
)
abstract class NovaDatabase : RoomDatabase() {
    abstract fun mediaDao(): MediaDao
    abstract fun playlistDao(): PlaylistDao

    companion object {
        @Volatile
        private var INSTANCE: NovaDatabase? = null

        fun getInstance(context: Context): NovaDatabase {
            return INSTANCE ?: synchronized(this) {
                val instance = Room.databaseBuilder(
                    context.applicationContext,
                    NovaDatabase::class.java,
                    "nova_player.db"
                ).fallbackToDestructiveMigration().build()
                INSTANCE = instance
                instance
            }
        }
    }
}
