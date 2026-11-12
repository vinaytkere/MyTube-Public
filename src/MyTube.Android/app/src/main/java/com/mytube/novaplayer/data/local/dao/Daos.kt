package com.mytube.novaplayer.data.local.dao

import androidx.room.*
import com.mytube.novaplayer.data.local.entity.MediaEntity
import com.mytube.novaplayer.data.local.entity.PlaylistEntity
import com.mytube.novaplayer.data.local.entity.PlaylistTrackEntity
import kotlinx.coroutines.flow.Flow

@Dao
interface MediaDao {
    @Query("SELECT * FROM media_items ORDER BY id DESC")
    fun getAllMedia(): Flow<List<MediaEntity>>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertAll(items: List<MediaEntity>): List<Long>

    @Query("DELETE FROM media_items")
    suspend fun clearAll(): Int

    @Query("DELETE FROM media_items WHERE id = :id")
    suspend fun deleteById(id: Long): Int
}

@Dao
interface PlaylistDao {
    @Query("SELECT * FROM playlists ORDER BY createdAtMs DESC")
    fun getAllPlaylists(): Flow<List<PlaylistEntity>>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertPlaylist(playlist: PlaylistEntity): Long

    @Query("UPDATE playlists SET name = :newName WHERE id = :playlistId")
    suspend fun updatePlaylistName(playlistId: Long, newName: String): Int

    @Query("DELETE FROM playlists WHERE id = :playlistId")
    suspend fun deletePlaylist(playlistId: Long): Int

    @Query("""
        SELECT m.* FROM media_items m
        INNER JOIN playlist_tracks pt ON m.id = pt.mediaId
        WHERE pt.playlistId = :playlistId
        ORDER BY pt.sortOrder ASC
    """)
    fun getTracksForPlaylist(playlistId: Long): Flow<List<MediaEntity>>

    @Insert(onConflict = OnConflictStrategy.IGNORE)
    suspend fun addTrackToPlaylist(entry: PlaylistTrackEntity): Long

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertTracks(tracks: List<PlaylistTrackEntity>): List<Long>

    @Query("DELETE FROM playlist_tracks WHERE playlistId = :playlistId AND mediaId = :mediaId")
    suspend fun removeTrackFromPlaylist(playlistId: Long, mediaId: Long): Int

    @Query("DELETE FROM playlist_tracks WHERE playlistId = :playlistId")
    suspend fun clearTracksForPlaylist(playlistId: Long): Int

    @Transaction
    suspend fun updateTrackOrder(playlistId: Long, orderedMediaIds: List<Long>) {
        clearTracksForPlaylist(playlistId)
        val entries = orderedMediaIds.mapIndexed { index, mediaId ->
            PlaylistTrackEntity(playlistId = playlistId, mediaId = mediaId, sortOrder = index)
        }
        insertTracks(entries)
    }
}
