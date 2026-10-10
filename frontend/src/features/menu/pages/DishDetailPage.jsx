import { useParams, Link } from "react-router-dom";
import { useFetch } from "../hooks/useFetch";
import { API_BASE_URL } from "../../../config/api";

function DishDetailPage() {
  const { id } = useParams(); // from /dish/:id
  const { data: dish, isLoading, error } = useFetch(`${API_BASE_URL}/api/menu/${id}`);

  return (
    <div>
      <p>Showing details for dish #{id}</p>
      {isLoading && <p>Loading dish details…</p>}
      {error && <p>Could not load dish details: {error}</p>}
      {dish && (
        <div className="dish-detail">
          <h2>{dish.name}</h2>
          <p>Price: Rs. {Number(dish.price).toFixed(2)}</p>
          <p>Category: {dish.category}</p>
          <p>Status: {dish.available ? "Available" : "Sold out"}</p>
        </div>
      )}
      <Link to="/">← Back to menu</Link>
    </div>
  );
}
export default DishDetailPage;
