using System.Collections.Generic;

namespace RetroMesh.Engine
{
    public static class ListCapacityHelper
    {
        public static void EnsureCapacity<T>(List<T> list, int capacity)
        {
            if (list.Capacity < capacity)
                list.Capacity = capacity;
        }
    }
}
