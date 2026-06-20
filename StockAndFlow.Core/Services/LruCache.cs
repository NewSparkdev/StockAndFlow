using System;
using System.Collections.Generic;

namespace StockAndFlow.Services
{
    /// <summary>
    /// A simple thread-safe LRU (Least Recently Used) cache with a maximum size.
    /// When the cache exceeds the maximum size, the least recently accessed item is evicted.
    /// </summary>
    /// <typeparam name="TKey">The type of keys in the cache</typeparam>
    /// <typeparam name="TValue">The type of values in the cache</typeparam>
    public class LruCache<TKey, TValue> where TKey : notnull
    {
        private readonly int _maxSize;
        private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _cache;
        private readonly LinkedList<CacheItem> _lruList;
        private readonly object _lock = new object();

        public LruCache(int maxSize)
        {
            if (maxSize <= 0)
                throw new ArgumentException("Max size must be greater than 0", nameof(maxSize));

            _maxSize = maxSize;
            _cache = new Dictionary<TKey, LinkedListNode<CacheItem>>(maxSize);
            _lruList = new LinkedList<CacheItem>();
        }

        /// <summary>
        /// Gets the current number of items in the cache.
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _cache.Count;
                }
            }
        }

        /// <summary>
        /// Gets the maximum size of the cache.
        /// </summary>
        public int MaxSize => _maxSize;

        /// <summary>
        /// Adds or updates an item in the cache.
        /// If the cache is full, the least recently used item is evicted.
        /// </summary>
        public void Set(TKey key, TValue value)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var existingNode))
                {
                    // Update existing item and move to front
                    existingNode.Value.Value = value;
                    _lruList.Remove(existingNode);
                    _lruList.AddFirst(existingNode);
                }
                else
                {
                    // Check if we need to evict
                    if (_cache.Count >= _maxSize)
                    {
                        // Remove least recently used item (tail of list)
                        var lruNode = _lruList.Last;
                        if (lruNode != null)
                        {
                            _lruList.RemoveLast();
                            _cache.Remove(lruNode.Value.Key);
                        }
                    }

                    // Add new item to front
                    var cacheItem = new CacheItem(key, value);
                    var node = _lruList.AddFirst(cacheItem);
                    _cache[key] = node;
                }
            }
        }

        /// <summary>
        /// Tries to get a value from the cache.
        /// If found, marks the item as recently used.
        /// </summary>
        public bool TryGetValue(TKey key, out TValue? value)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var node))
                {
                    // Move to front (most recently used)
                    _lruList.Remove(node);
                    _lruList.AddFirst(node);

                    value = node.Value.Value;
                    return true;
                }

                value = default;
                return false;
            }
        }

        /// <summary>
        /// Checks if a key exists in the cache.
        /// </summary>
        public bool ContainsKey(TKey key)
        {
            lock (_lock)
            {
                return _cache.ContainsKey(key);
            }
        }

        /// <summary>
        /// Removes an item from the cache.
        /// </summary>
        public bool Remove(TKey key)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var node))
                {
                    _lruList.Remove(node);
                    _cache.Remove(key);
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Clears all items from the cache.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _cache.Clear();
                _lruList.Clear();
            }
        }

        private class CacheItem
        {
            public TKey Key { get; }
            public TValue Value { get; set; }

            public CacheItem(TKey key, TValue value)
            {
                Key = key;
                Value = value;
            }
        }
    }
}
