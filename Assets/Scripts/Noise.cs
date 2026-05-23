using UnityEngine;

public class Noise
{
    public static float[,] GenerateNoiseMap(int mapWidth, int mapHeight,int seed, float Scale , int octaves , float persistance , float lucanarity , Vector2 offset)
    {
        float[,] noiseMap = new float[mapWidth, mapHeight];
        System.Random prng = new System.Random(seed);
        Vector2[] octaveOffsets = new Vector2[octaves];
        for(int i = 0; i < octaves; i++)
        {
            float offsetX = prng.Next(-100000,100000) + offset.x;
            float offsetY = prng.Next(-100000, 100000) + offset.y;
            octaveOffsets[i] = new Vector2(offsetX, offsetY);
        }
        if (Scale == 0)
        {
            Scale = 0.001f;
        }


        float maxNoiseHeight = float.MinValue;
        float minNoiseHeight = float.MaxValue;

        float halfWidth = mapWidth / 2f;
        float halfHeight = mapHeight / 2f;
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                float amplitude = 1;
                float frequency =1;
                float noiseHeight = 0;
                for(int i=0;i<octaves; i++)
                {
                    float sampleX = (x-halfWidth) / Scale * frequency + octaveOffsets[i].x;
                    float sampleY = (y-halfHeight) / Scale * frequency + octaveOffsets[i].y;
                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleY);
                    noiseHeight += perlinValue*amplitude ;
                    amplitude *= persistance;
                    frequency *= lucanarity;
                }
                if (noiseHeight > maxNoiseHeight)
                {
                    maxNoiseHeight = noiseHeight;
                }else if(noiseHeight < minNoiseHeight)
                {
                    minNoiseHeight = noiseHeight;
                }
                    noiseMap[x, y] = noiseHeight;
                
                
            }
        }

        for (int y = 0; y < mapHeight; y++) { 
            for (int x = 0; x < mapWidth; x++)
            {
                noiseMap[x,y] = Mathf.InverseLerp(minNoiseHeight,maxNoiseHeight,noiseMap[x,y]);
            }
        }
        return noiseMap;
    }
  
}
