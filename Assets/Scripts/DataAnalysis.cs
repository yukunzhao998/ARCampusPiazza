using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DataAnalysis : MonoBehaviour
{
    public static Vector3 getAccuratePosition(List<Vector3> hisPosition)
    {
        Vector3 avg = getAvg(hisPosition);
        Vector3 stdev = getStdev(hisPosition);

        List<Vector3> accuratePosition = new List<Vector3>();

        foreach (var item in hisPosition)
        {
            if(judgePosition(item, avg, stdev))
            {
                accuratePosition.Add(item);
            }
        }
        Vector3 result = getAvg(accuratePosition);
        return result;
    }

    public static Vector3 getAvg(List<Vector3> hisPosition)
    {
        int num = hisPosition.Count;
        float sum_x = 0, sum_y = 0, sum_z =0;

        foreach (var item in hisPosition)
        {
            sum_x += item[0];
            sum_y += item[1];
            sum_z += item[2];
        }
        float avg_x = sum_x/num;
        float avg_y = sum_y/num;
        float avg_z = sum_z/num;

        Vector3 result = new Vector3(avg_x, avg_y, avg_z);
        return result;
    }

    public static Vector3 getStdev(List<Vector3> hisPosition)
    {
        int num = hisPosition.Count;
        float sum_x = 0, sum_y = 0, sum_z =0;

        foreach (var item in hisPosition)
        {
            sum_x += item[0];
            sum_y += item[1];
            sum_z += item[2];
        }
        float avg_x = sum_x/num;
        float avg_y = sum_y/num;
        float avg_z = sum_z/num;

        float sum_x_square = 0, sum_y_square = 0, sum_z_square = 0;
        foreach (var item in hisPosition)
        {
            sum_x_square += (item[0]-avg_x)*(item[0]-avg_x);
            sum_y_square += (item[1]-avg_y)*(item[1]-avg_y);
            sum_z_square += (item[2]-avg_z)*(item[2]-avg_z);
        }
        float stdev_x = (float)Math.Sqrt(sum_x_square/num);
        float stdev_y = (float)Math.Sqrt(sum_y_square/num);
        float stdev_z = (float)Math.Sqrt(sum_z_square/num);

        Vector3 result = new Vector3(stdev_x, stdev_y, stdev_z);
        return result;
    }

    public static bool judgePosition(Vector3 pos, Vector3 avg, Vector3 stdev)
    {
        //change the coefficient here
        int n = 2;

        if((pos[0]>=avg[0]-n*stdev[0])&&(pos[0]<=avg[0]+n*stdev[0])&&(pos[1]>=avg[1]-n*stdev[1])&&(pos[1]<=avg[1]+n*stdev[1])&&(pos[2]>=avg[2]-n*stdev[2])&&(pos[2]<=avg[2]+n*stdev[2]))
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}
