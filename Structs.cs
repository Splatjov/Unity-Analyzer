namespace Unity_Analyzer;

public struct Component
{
    public string fileID;
}
public struct GameObject
{
    public string m_Name;
    public int m_Layer;
    public List<Component> m_Component;
};

public struct GameObjectWrapper
{
    public GameObject GameObject;
};

public struct SceneRoots
{
    public List<Component> m_Roots;
};

public struct SceneRootsWrapper
{
    public SceneRoots SceneRoots;
};


public struct Transform
{
    public Component m_GameObject;
    public Component m_Father;
    public List<Component> m_Children;
};
public struct TransformWrapper
{
    public Transform Transform;
};

public struct CsFileWrapper
{
    public string guid;
};