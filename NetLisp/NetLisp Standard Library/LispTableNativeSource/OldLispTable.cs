/*using NetLisp.Data;
using NetLisp.Structs;
using System.Collections;

namespace LispTableNativeSource
{
    // hash table implementation for lisp types
    // or so it was supposed to be, until i added async stuff and now im just using a
    // ConcurrentDictionary:( but i was sooo proud of my little hash table i am just gonna keep it here.
    // the thought occurred to also write my own concurrent hash table...but that seems like quite a
    // terrible idea. honestly writing the hash table myself when Dictionary exists was pretty stupid.
    public class LispTable : ExtendedLispToken, IEnumerable<LispKeyValuePair>, IEnumerable
    {
        public static ExtendedTypeInfo TableExtendedTypeInfo { get; } = new ExtendedTypeInfo()
        {
            ExtendedTypeGuid = new Guid("3347cb5b-f285-4b9a-873c-fbf3fe480eb3"),
            ExtendedTypeName = "table"
        };

        public override ExtendedTypeInfo ExtendedTypeInfo => LispTable.TableExtendedTypeInfo;

        // TODO: set to private const after done testing
        public int bucketMaxSize = 100000;
        public int bucketInitialSize = 100;
        public float fillThreshold = 0.75F;
        public float growFactor = 2F;

        public LispTable()
        {
            bucket = new HashBucket[bucketInitialSize];
            for (int i = 0; i < bucketInitialSize; i++)
            {
                bucket[i] = new HashBucket();
            }
            bucketSize = bucketInitialSize;
        }

        private HashBucket[] bucket;
        private int bucketSize;
        private int keyCount = 0;

        public LispToken? this[LispToken key]
        {
            get
            {
                LispKeyValuePair entry = getBucketForKey(key).Search(key);
                if (entry == null)
                {
                    return null;
                }
                return entry.Value;
            }
            set
            {
                bool added;
                LispKeyValuePair entry = getBucketForKey(key).SearchOrAdd(key, out added);
                entry.Value = value;
                if (added)
                {
                    keyCount++;
                    checkGrow();
                }
            }
        }
        public bool ContainsKey(LispToken key)
        {
            return getBucketForKey(key).Search(key) != null;
        }
        public bool DeleteKey(LispToken key)
        {
            if (getBucketForKey(key).SearchDelete(key))
            {
                keyCount--;
                return true;
            } else
            {
                return false;
            }
        }

        private void checkGrow()
        {
            if (keyCount / (float)bucketSize >= fillThreshold)
            {
                resizeBucket((int)(bucketSize * growFactor));
            }
        }

        private void resizeBucket(int newSize)
        {
            LispKeyValuePair[] tableContent = new LispKeyValuePair[keyCount];
            int tableContentI = 0;
            foreach (LispKeyValuePair keyValuePair in this)
            {
                tableContent[tableContentI] = keyValuePair;
                tableContentI++;
            }
            bucketSize = newSize;
            bucket = new HashBucket[bucketSize];
            for (int i = 0; i < bucketSize; i++)
            {
                bucket[i] = new HashBucket();
            }
            foreach (LispKeyValuePair keyValuePair in tableContent)
            {
                getBucketForKey(keyValuePair.Key).HashMatchItems.Add(keyValuePair);
            }
        }

        private HashBucket getBucketForKey(LispToken key)
        {
            uint hash = (uint)key.HashValue();
            return bucket[hash % bucketSize];
        }

        public override bool CompareValue(LispToken token)
        {
            return this == token; // reference
        }

        public override int HashValue()
        {
            return this.GetHashCode(); // reference
        }

        public IEnumerator<LispKeyValuePair> GetEnumerator()
        {
            return new LispTableEnumerator(bucket);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return new LispTableEnumerator(bucket);
        }

        private class LispTableEnumerator : IEnumerator<LispKeyValuePair>, IEnumerator
        {
            public LispKeyValuePair Current => currentBucketItem.HashMatchItems[currentIndex];

            object IEnumerator.Current => currentBucketItem.HashMatchItems[currentIndex];

            private HashBucket[] bucket;
            private HashBucket currentBucketItem;
            private int currentBucketIndex;
            private int currentIndex;

            public LispTableEnumerator(HashBucket[] bucket)
            {
                this.bucket = bucket;
                Reset();
            }

            public void Dispose()
            {
                bucket = null;
                currentBucketItem = null;
            }

            public bool MoveNext()
            {
                currentIndex++;
                if (currentIndex < currentBucketItem.HashMatchItems.Count - 1)
                {
                    return true;
                } else
                {
                    while (currentIndex >= currentBucketItem.HashMatchItems.Count)
                    {
                        currentBucketIndex++;
                        if (currentBucketIndex < bucket.Length)
                        {
                            currentIndex = 0;
                            currentBucketItem = bucket[currentBucketIndex];
                        }
                        else
                        {
                            return false;
                        }
                    }
                    return true;
                }
            }

            public void Reset()
            {
                currentBucketIndex = 0;
                currentBucketItem = bucket[0];
                currentIndex = -1;
            }
        }

        private class HashBucket
        {
            public List<LispKeyValuePair> HashMatchItems = new List<LispKeyValuePair>();
            public LispKeyValuePair? Search(LispToken key)
            {
                foreach (LispKeyValuePair checkPair in HashMatchItems)
                {
                    if (checkPair.Key.CompareValue(key))
                    {
                        return checkPair;
                    }
                }
                return null;
            }
            public LispKeyValuePair? SearchOrAdd(LispToken key, out bool added)
            {
                foreach (LispKeyValuePair checkPair in HashMatchItems)
                {
                    if (checkPair.Key.CompareValue(key))
                    {
                        added = false;
                        return checkPair;
                    }
                }
                LispKeyValuePair newKey = new LispKeyValuePair(key, null);
                HashMatchItems.Add(newKey);
                added = true;
                return newKey;
            }
            public bool SearchDelete(LispToken key)
            {
                int delI = -1;
                for (int i = 0; i < HashMatchItems.Count; i++)
                {
                    if (HashMatchItems[i].Key.CompareValue(key))
                    {
                        delI = i;
                        break;
                    }
                }
                if (delI < 0)
                {
                    return false;
                }
                HashMatchItems.RemoveAt(delI);
                return true;
            }
        }
    }
    public class LispKeyValuePair
    {
        public LispToken Key { get; set; }
        public LispToken Value { get; set; }
        public LispKeyValuePair(LispToken key, LispToken value)
        {
            Key = key;
            Value = value;
        }
    }
}*/