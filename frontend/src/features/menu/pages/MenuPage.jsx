import { useState } from "react";
import MenuList from "../components/MenuList";
import { useDebounce } from "../hooks/useDebounce";
import { useFetch } from "../hooks/useFetch";
import { API_BASE_URL } from "../../../config/api";

function MenuPage() {
  const [query, setQuery] = useState("");
  const debounced = useDebounce(query, 400);
  const { data: dishes, isLoading, error } = useFetch(`${API_BASE_URL}/api/menu`);
  const items = Array.isArray(dishes) ? dishes : dishes?.menuItems || [];
  const filtered = items.filter((d) =>
    d.name?.toLowerCase().includes(debounced.toLowerCase()),
  );

  if (isLoading) return <p>Loading menu…</p>;
if (error) return <p>Could not load menu: {error}</p>;
  return (
    <div>
      <input
        value={query}
        onChange={(e) => setQuery(e.target.value)}
        placeholder="Search dishes..."
      />
      <MenuList dishes={filtered} />
    </div>
  );
}

export default MenuPage;
