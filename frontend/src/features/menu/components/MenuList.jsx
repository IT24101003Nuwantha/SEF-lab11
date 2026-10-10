import DishCard from "./DishCard";
function MenuList({ dishes }) {
  if (!dishes.length) return <p>No dishes match your search.</p>;
  return (
    <div className="menu-grid">
      {dishes.map((dish) => (
        <DishCard key={dish.id} dish={dish} />
      ))}
    </div>
  );
}
export default MenuList;
