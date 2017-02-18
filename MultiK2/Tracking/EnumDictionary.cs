using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiK2.Tracking
{
    internal class EnumDictionary<T> : IReadOnlyDictionary<JointType, T>
    {
        private T[] _jointData;

        internal EnumDictionary(T[] jointData)
        {
            _jointData = jointData;
        }

        public T this[JointType key] => _jointData[(int)key];

        public int Count => _jointData.Length;

        public IEnumerable<JointType> Keys => (JointType[])Enum.GetValues(typeof(JointType));

        public IEnumerable<T> Values => _jointData;

        public bool ContainsKey(JointType key) => true;

        public IEnumerator<KeyValuePair<JointType, T>> GetEnumerator()
        {
            for (var i = 0; i < _jointData.Length; i++)
            {
                yield return new KeyValuePair<JointType, T>((JointType)i, _jointData[i]);
            }
        }

        public bool TryGetValue(JointType key, out T value)
        {
            value = _jointData[(int)key];
            return true;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
