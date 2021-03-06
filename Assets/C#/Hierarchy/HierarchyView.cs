﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Battlehub.UIControls;

public interface ISelectHandler
{
    void OnSelected();
    void OnUnselected();
}

/// <summary>
/// In this demo we use game objects hierarchy as data source (each data item is game object)
/// You can use any hierarchical data with treeview.
/// </summary>
public class HierarchyView : MonoBehaviour
{
    private static HierarchyView m_Instance;
    public TreeView TreeView;
    public List<GameObject> Root;

    public static List<GameObject> GetRoot()
    {
        return m_Instance.Root;
    }

    public static bool IsCreated(GameObject obj)
    {
        return m_Instance.TreeView.GetTreeViewItem(obj) != null;
    }

    public static bool IsPrefab( Transform This )
    {
        if (Application.isEditor && !Application.isPlaying)
        {
            throw new InvalidOperationException( "Does not work in edit mode" );
        }
        return This.gameObject.scene.buildIndex < 0;
    }

    private void Awake()
    {
        m_Instance = this;
    }

    private void Start()
    {
        if (!TreeView)
        {
            Debug.LogError( "Set TreeView field" );
            return;
        }

        IEnumerable<GameObject> dataItems = Root;

        //subscribe to events
        TreeView.ItemDataBinding += OnItemDataBinding;
        TreeView.SelectionChanged += OnSelectionChanged;
        TreeView.ItemsRemoved += OnItemsRemoved;
        TreeView.ItemExpanding += OnItemExpanding;
        TreeView.ItemBeginDrag += OnItemBeginDrag;

        TreeView.ItemDrop += OnItemDrop;
        TreeView.ItemBeginDrop += OnItemBeginDrop;
        TreeView.ItemEndDrag += OnItemEndDrag;

        TreeView.CanItemRemove = ( item ) => !Root.Contains( (GameObject) item );

        //Bind data items
        TreeView.Items = dataItems;
    }

    public static bool Select( GameObject obj )
    {
        var newSelected = new List<object>();
        if (m_Instance.TreeView.IsItemSelected( obj ))
        {
            newSelected.AddRange( m_Instance.TreeView.SelectedItems.Cast<object>().ToList() );
            newSelected.Remove( obj );
            m_Instance.TreeView.SelectedItems = newSelected;
            return false;
        }
        if (Input.GetKey( m_Instance.TreeView.MultiselectKey ) && m_Instance.TreeView.SelectedItems != null)
            newSelected.AddRange( m_Instance.TreeView.SelectedItems.Cast<object>().ToList() );
        newSelected.Add( obj );
        m_Instance.TreeView.SelectedItems = newSelected;
        return true;
    }

    public static void Remove(GameObject obj)
    {
        if (obj.transform.parent == null) return;
        var parent = obj.transform.parent.gameObject;
        m_Instance.TreeView.RemoveChild(parent,obj, parent.transform.childCount == 1);
    }

    private void OnItemBeginDrop( object sender, ItemDropCancelArgs e )
    {
        GameObject dropTarget = (GameObject) e.DropTarget;
        if (Root.Contains( dropTarget ) && ( e.Action == ItemDropAction.SetNextSibling || e.Action == ItemDropAction.SetPrevSibling ))
        {
            e.Cancel = true;
        }

    }

    public static List<CUIObject> GetCurrent()
    {
        var elements = new List<CUIObject>();
        foreach (var root in m_Instance.Root)
        {
            elements.AddRange(root.GetComponentsInChildren<CUIObject>());
        }
        return elements;
    }

    private void OnDestroy()
    {
        if (!TreeView)
        {
            return;
        }


        //unsubscribe
        TreeView.ItemDataBinding -= OnItemDataBinding;
        TreeView.SelectionChanged -= OnSelectionChanged;
        TreeView.ItemsRemoved -= OnItemsRemoved;
        TreeView.ItemExpanding -= OnItemExpanding;
        TreeView.ItemBeginDrag -= OnItemBeginDrag;
        TreeView.ItemBeginDrop -= OnItemBeginDrop;
        TreeView.ItemDrop -= OnItemDrop;
        TreeView.ItemEndDrag -= OnItemEndDrag;

        TreeView.CanItemRemove = null;
    }

    private void OnItemExpanding( object sender, ItemExpandingArgs e )
    {
        //get parent data item (game object in our case)
        GameObject gameObject = (GameObject) e.Item;
        if (gameObject.transform.childCount > 0)
        {
            //get children
            List<GameObject> children = new List<GameObject>();
            foreach (Transform child in gameObject.transform)
                if (child.tag == "CUI")
                    children.Add( child.gameObject );
            //Populate children collection
            e.Children = children;
        }
    }

    public static IEnumerable<GameObject> GetSelectedItems()
    {
        if (m_Instance.TreeView.SelectedItems == null) return new List<GameObject>();
        else return m_Instance.TreeView.SelectedItems.OfType<GameObject>();
    }

    public static void ChangeName( GameObject item, string name )
    {
        item.name = name;
        var tvi = m_Instance.TreeView.GetItemContainer( item );
        if (tvi != null)
        {
            tvi.GetComponentInChildren<Text>().text = name;
        }
    }

    public static void AddSelectionListener( EventHandler<SelectionChangedArgs> callback )
    {
        m_Instance.TreeView.SelectionChanged += callback;
    }


    private void OnSelectionChanged( object sender, SelectionChangedArgs e )
    {
#if UNITY_EDITOR
        //Do something on selection changed (just syncronized with editor's hierarchy for demo purposes)
        UnityEditor.Selection.objects = e.NewItems.OfType<GameObject>().ToArray();
#endif
        foreach (var oldItem in e.OldItems.Cast<GameObject>())
        {
            SendSelectEvent( oldItem, ( i ) => i.OnUnselected() );
        }
        foreach (var newItem in e.NewItems.Cast<GameObject>())
        {
            SendSelectEvent( newItem, ( i ) => i.OnSelected() );
        }
        if (e.NewItems.Any( p => Root.Contains( (GameObject) p ) ))
        {
            TreeView.SelectedItems = e.NewItems.Where( p => !Root.Contains( (GameObject) p ) );
            return;
        }
    }

    public void SendSelectEvent( GameObject obj, Action<ISelectHandler> callback )
    {
        foreach (var selectHandler in obj.GetComponents( typeof( ISelectHandler ) ).OfType<ISelectHandler>())
        {
            callback.Invoke( selectHandler );
        }
    }

    private void OnItemsRemoved( object sender, ItemsRemovedArgs e )
    {
        //Destroy removed dataitems
        for (int i = 0; i < e.Items.Length; ++i)
        {
            GameObject go = (GameObject) e.Items[ i ];
            if (go != null)
            {
                PoolManager.Release( PrefabType.Cui, go );
            }
        }
    }


    public static void OnRename( GameObject item )
    {
        m_Instance.TreeView.GetItemContainer( item ).GetComponentInChildren<Text>().text = item.name;
    }

    /// <summary>
    /// This method called for each data item during databinding operation
    /// You have to bind data item properties to ui elements in order to display them.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnItemDataBinding( object sender, TreeViewItemDataBindingArgs e )
    {
        GameObject dataItem = e.Item as GameObject;
        if (dataItem != null)
        {
            //We display dataItem.name using UI.Text 
            Text text = e.ItemPresenter.GetComponentInChildren<Text>( true );
            text.text = dataItem.name;

            //LoadInternal icon from resources
            Image icon = e.ItemPresenter.GetComponentsInChildren<Image>()[ 4 ];
            icon.sprite = Resources.Load<Sprite>( "cube" );

            //And specify whether data item has children (to display expander arrow if needed)
            if (dataItem.name != "TreeView")
            {
                e.CanDrag = e.CanEdit = !Root.Contains( dataItem );
                e.HasChildren = Hierarchy.Lookup[ dataItem ].GetChildrenCount() > 0;
            }
        }
    }

    private void OnItemBeginDrag( object sender, ItemArgs e )
    {
        //Could be used to change cursor
    }

    private void OnItemDrop( object sender, ItemDropArgs e )
    {
        if (e.DropTarget == null)
        {
            return;
        }

        Transform dropT = ( (GameObject) e.DropTarget ).transform;
        //Set drag items as children of drop target
        if (e.Action == ItemDropAction.SetLastChild)
        {
            for (int i = 0; i < e.DragItems.Length; ++i)
            {
                Transform dragT = ( (GameObject) e.DragItems[ i ] ).transform;
                Hierarchy.Lookup[ dragT.gameObject ].SetParent( dropT.gameObject );
                dragT.SetAsLastSibling();
            }
        }

        //Put drag items next to drop target
        else if (e.Action == ItemDropAction.SetNextSibling)
        {
            for (int i = e.DragItems.Length - 1; i >= 0; --i)
            {
                Transform dragT = ( (GameObject) e.DragItems[ i ] ).transform;
                int dropTIndex = dropT.GetSiblingIndex();
                if (dragT.parent != dropT.parent)
                {
                    Hierarchy.Lookup[ dragT.gameObject ].SetParent( dropT.parent.gameObject );
                    dragT.SetSiblingIndex( dropTIndex + 1 );
                }
                else
                {
                    int dragTIndex = dragT.GetSiblingIndex();
                    if (dropTIndex < dragTIndex)
                    {
                        dragT.SetSiblingIndex( dropTIndex + 1 );
                    }
                    else
                    {
                        dragT.SetSiblingIndex( dropTIndex );
                    }
                }
            }
        }

        //Put drag items before drop target
        else if (e.Action == ItemDropAction.SetPrevSibling)
        {
            for (int i = 0; i < e.DragItems.Length; ++i)
            {
                Transform dragT = ( (GameObject) e.DragItems[ i ] ).transform;
                if (dragT.parent != dropT.parent)
                {
                    Hierarchy.Lookup[ dragT.gameObject ].SetParent( dropT.parent.gameObject );
                }

                int dropTIndex = dropT.GetSiblingIndex();
                int dragTIndex = dragT.GetSiblingIndex();
                if (dropTIndex > dragTIndex)
                {
                    dragT.SetSiblingIndex( dropTIndex - 1 );
                }
                else
                {
                    dragT.SetSiblingIndex( dropTIndex );
                }
            }
        }
    }

    private void OnItemEndDrag( object sender, ItemArgs e )
    {

    }


    public static void ChangeParent(GameObject newParent, GameObject item)
    {
        m_Instance.TreeView.ChangeParent(newParent, item);
        if (m_Instance.TreeView.GetTreeViewItem(newParent) == null) return;

        m_Instance.TreeView.GetTreeViewItem( newParent ).CanExpand = true;
        m_Instance.TreeView.GetTreeViewItem( newParent ).IsExpanded = true;
    }

    public static void AddChild( GameObject obj )
    {
        m_Instance.TreeView.AddChild( m_Instance.Root[ 1 ], obj );
    }
}