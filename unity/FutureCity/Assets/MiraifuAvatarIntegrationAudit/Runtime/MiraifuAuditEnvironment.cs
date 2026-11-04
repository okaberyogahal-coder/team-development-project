using UnityEngine;
using FutureCity;
using MiraifuAvatarPrototype;
namespace MiraifuAvatarIntegrationAudit
{
    // Reuses geometry generators only: no MiraiWorld, session request, traffic, or production bootstrap.
    [DefaultExecutionOrder(-300)]
    public sealed class MiraifuAuditEnvironment : MonoBehaviour
    {
        public bool actualCity;public int seed,gridSize;public float blockSize,roadWidth,maxHeight,parkChance;public bool photoMode;
        public Material[] bakedMaterials;public Material bakedSkybox;public MiraifuRuntimeAvatarLab lab;
        public CityGenerator City{get;private set;}public Vector3 Spawn=>actualCity?City.Campus.Spawn:Vector3.zero;
        void Awake()
        {
            if(!actualCity)return;var library=MaterialLibrary.Create(bakedMaterials,bakedSkybox,seed);
            City=new GameObject("Read-only FutureCity geometry").AddComponent<CityGenerator>();City.transform.SetParent(transform,false);City.seed=seed;City.gridSize=gridSize;City.blockSize=blockSize;City.roadWidth=roadWidth;City.maxHeight=maxHeight;City.parkChance=parkChance;City.photoMode=photoMode;City.generateColliders=true;
            bool cards=photoMode&&library.HasPhotoTextures;MeshBuilder.BevelSize=photoMode?.025f:0;if(cards)Foliage.Begin();City.Generate(library);MiraiPark.Build(library,City,transform);if(cards)Foliage.End(library,transform);MeshBuilder.BevelSize=0;
        }
        void Start(){lab.GetComponent<PlayerWalker>().Place(Spawn,actualCity?City.Campus.SpawnYaw:0);}
    }
}
