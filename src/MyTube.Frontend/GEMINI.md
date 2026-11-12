# Frontend Development Rules & Standards (Vercel Guidelines)

When reading, creating, or modifying code in `MyTube.Frontend`:

## 1. Performance & React Patterns
- **Waterfalls**: Never chain sequential `await`s when promises are independent. Use `Promise.all([fetchA(), fetchB()])`.
- **Imports**: Avoid barrel imports (e.g., direct imports from lucide-react or granular component modules).
- **Derived State**: Compute derived state during render; never sync props or values into state with `useEffect`.
- **Callbacks**: Use functional `setState(prev => ...)` to avoid re-instantiating callback dependencies.
- **Defer Work**: For real-time search/filter inputs, use `useDeferredValue` or `startTransition` to keep input responsive.
- **Conditionals**: Use explicit ternaries (`condition ? <Component /> : null`) instead of `condition && <Component />` to prevent rendering falsy zero (`0`) or NaN.
- **Sub-components**: Never declare component functions inside the body of another component.

## 2. Accessibility & Web Interface
- **Focus Rings**: Never use `outline-none` without an explicit `:focus-visible:ring-2` replacement.
- **Labels & A11y**:
  - Every icon-only button must have an `aria-label`.
  - Decorative icons must have `aria-hidden="true"`.
  - All form controls must be linked to a `<label htmlFor="...">` or have `aria-label`.
- **Buttons vs Links**: Use `<button>` for actions and `<a>` / `<Link>` for navigation. Never `<div onClick>`.
- **Flex Truncation**: Any flex child with text truncation (`truncate`, `line-clamp-*`) must include `min-w-0`.
- **Touch Targets**: Minimum hit area is 44x44px for interactive buttons on touch screens; add `touch-action: manipulation`.

## 3. Styling & Tailwind
- Use Tailwind CSS utility classes aligned with design tokens.
- Explicit transitions (e.g. `transition-colors`, `transition-opacity`) instead of `transition-all`.
- Honor `prefers-reduced-motion` for animations.
