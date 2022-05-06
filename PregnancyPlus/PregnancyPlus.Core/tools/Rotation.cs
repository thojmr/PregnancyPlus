using UnityEngine;
using System;

namespace KK_PregnancyPlus
{
    //Just some usefull vector/quaternion rotation methods
    public static class Rotation
    {

        /// <summary>
        /// Check the approximate equality of two quaternions by some range
        /// </summary>  
        public static bool Approximately(this Quaternion quatA, Quaternion value, float acceptableRange)
        {
            return 1 - Mathf.Abs(Quaternion.Dot(quatA, value)) < acceptableRange;
        } 


        /// <summary>
        /// Rounds a Vector3 euler to the closest 90 degree axis for each direction
        /// </summary>
        /// <param name="vector">Vector to round</param>
        /// <param name="degrees">The degree to round to</param>
        /// <returns></returns>
        public static Vector3 AxisRound(Vector3 vector, int degrees = 90)
        {
            //For each direction, round to nearest 90 degrees, and keep it within 360 degrees
            vector.x = (float)Math.Round((double)vector.x/degrees, MidpointRounding.AwayFromZero) * degrees % 360;
            vector.y = (float)Math.Round((double)vector.y/degrees,MidpointRounding.AwayFromZero) * degrees % 360;
            vector.z = (float)Math.Round((double)vector.z/degrees, MidpointRounding.AwayFromZero) * degrees % 360;

            return vector;
        }


        //An overload for AxisRound() above that takes a quaternion
        public static Quaternion AxisRound(Quaternion quaternion)
        {
            var roundedAxis = AxisRound(quaternion.eulerAngles);
            return Quaternion.Euler(roundedAxis);
        }
    }
}