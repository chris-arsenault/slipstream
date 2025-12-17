using SkiaSharp;

namespace Slipstream.UI.Components;

/// <summary>
/// Container for managing and rendering multiple UI components.
/// </summary>
public class ComponentContainer
{
    private readonly List<IUIComponent> _components = new();
    private readonly Dictionary<string, IUIComponent> _componentsById = new();

    /// <summary>
    /// Adds a component to the container.
    /// </summary>
    public void Add(IUIComponent component)
    {
        _components.Add(component);
        _componentsById[component.Id] = component;
    }

    /// <summary>
    /// Removes a component by ID.
    /// </summary>
    public bool Remove(string id)
    {
        if (_componentsById.TryGetValue(id, out var component))
        {
            _components.Remove(component);
            _componentsById.Remove(id);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets a component by ID.
    /// </summary>
    public IUIComponent? Get(string id)
    {
        return _componentsById.TryGetValue(id, out var component) ? component : null;
    }

    /// <summary>
    /// Gets a component by ID with type casting.
    /// </summary>
    public T? Get<T>(string id) where T : class, IUIComponent
    {
        return _componentsById.TryGetValue(id, out var component) ? component as T : null;
    }

    /// <summary>
    /// Clears all components.
    /// </summary>
    public void Clear()
    {
        _components.Clear();
        _componentsById.Clear();
    }

    /// <summary>
    /// Renders all visible components.
    /// </summary>
    public void Render(SKCanvas canvas, UITheme theme)
    {
        foreach (var component in _components)
        {
            if (component.IsVisible)
            {
                component.Render(canvas, theme);
            }
        }
    }

    /// <summary>
    /// Handles a click and returns any triggered action.
    /// </summary>
    public UIAction? HandleClick(SKPoint point)
    {
        // Check components in reverse order (top-most first)
        for (int i = _components.Count - 1; i >= 0; i--)
        {
            var component = _components[i];
            if (component.IsVisible && component.HitTest(point))
            {
                var action = component.HandleClick(point);
                if (action != null)
                {
                    return action;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Gets all components.
    /// </summary>
    public IEnumerable<IUIComponent> GetAll() => _components;

    /// <summary>
    /// Gets the count of components.
    /// </summary>
    public int Count => _components.Count;
}
