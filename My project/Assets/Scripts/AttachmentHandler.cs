using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttachmentHandler : MonoBehaviour
{
    [Header("Attachment Settings")]
    public float attachRange = 0.5f;
    public LayerMask itemLayer;
    public Vector2 detectionPadding = new Vector2(0.5f, 0.5f);
    public Vector2 detectionOffset = Vector2.zero;

    private List<GameObject> rightPackages = new List<GameObject>();
    private List<GameObject> leftPackages = new List<GameObject>();
    private List<GameObject> topPackages = new List<GameObject>();
    private bool canToggleAttach = true;
    private bool hasPackageInRange = false;

    public enum AttachmentSide { Right, Left, Top }

    public void ToggleAttachment(AttachmentSide side)
    {
        List<GameObject> packageList = GetPackageList(side);

        if (packageList.Count >= 1)
        {
            DetachLastItem(packageList);
            return;
        }

        Bounds detectionBounds = GetDetectionBounds();
        Collider2D[] hits = Physics2D.OverlapBoxAll(detectionBounds.center, detectionBounds.size, 0f, itemLayer);

        hasPackageInRange = hits.Length > 0;

        foreach (Collider2D col in hits)
        {
            GameObject item = col.gameObject;

            if (!packageList.Contains(item))
            {
                Vector2 attachPos = GetAttachAttachPosition(side);
                AttachItem(item, attachPos, packageList);
                break;
            }
        }

        StartCoroutine(AttachCooldown());
    }

    List<GameObject> GetPackageList(AttachmentSide side)
    {
        return side switch
        {
            AttachmentSide.Right => rightPackages,
            AttachmentSide.Left => leftPackages,
            AttachmentSide.Top => topPackages,
            _ => rightPackages
        };
    }

        Bounds GetDetectionBounds()
    {
        Vector3 basePos = transform.position + (Vector3)detectionOffset;
        Vector2 size = GetVisualBoundsSize() + detectionPadding * 2f;
        return new Bounds(basePos, new Vector3(size.x, size.y, 1f));
    }


    Vector2 GetVisualBoundsSize()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        return sr != null ? sr.bounds.size : Vector2.one;
    }

    Vector2 GetAttachAttachPosition(AttachmentSide side)
    {
        Vector2 basePos = transform.position;
        Vector2 halfSize = GetVisualBoundsSize() / 2f;

        return side switch
        {
            AttachmentSide.Right => basePos + new Vector2(halfSize.x + attachRange, 0),
            AttachmentSide.Left => basePos - new Vector2(halfSize.x + attachRange, 0),
            AttachmentSide.Top => basePos + new Vector2(0, halfSize.y + attachRange),
            _ => basePos
        };
    }

    void AttachItem(GameObject item, Vector2 attachCenter, List<GameObject> packageList)
    {
        item.transform.SetParent(transform);
        item.transform.rotation = Quaternion.identity;
        item.transform.position = attachCenter;

        Rigidbody2D rb = item.GetComponent<Rigidbody2D>();
        if (rb != null) rb.simulated = false;

        packageList.Add(item);
    }

    void DetachLastItem(List<GameObject> packageList)
    {
        GameObject last = packageList[^1];
        packageList.RemoveAt(packageList.Count - 1);

        last.transform.SetParent(null);

        Rigidbody2D rb = last.GetComponent<Rigidbody2D>();
        if (rb != null) rb.simulated = true;
    }

    IEnumerator AttachCooldown()
    {
        canToggleAttach = false;
        yield return new WaitForSeconds(0.2f);
        canToggleAttach = true;
    }

    void OnDrawGizmosSelected()
    {
        Bounds bounds = GetDetectionBounds();
        float markerSize = 0.3f;

        Gizmos.color = hasPackageInRange ? new Color(0f, 1f, 0f, 0.2f) : new Color(1f, 0.5f, 0.5f, 0.2f);
        Gizmos.DrawCube(bounds.center, bounds.size);

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(GetAttachAttachPosition(AttachmentSide.Right), Vector3.one * markerSize);
        Gizmos.DrawWireCube(GetAttachAttachPosition(AttachmentSide.Left), Vector3.one * markerSize);
        Gizmos.DrawWireCube(GetAttachAttachPosition(AttachmentSide.Top), Vector3.one * markerSize);
    }
}
