
import { useEffect, useState } from "react"
// import reactLogo from './assets/react.svg'
// import viteLogo from '/vite.svg'
import './App.css'

type Product = {
  id: string;
  name: string;
  description?: string;
  unitPrice: number;
  isActive: boolean;
  createdAt: string;
};

function App() {
  const [products, setProducts] = useState<Product[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    async function loadProducts() {
      try {
        const response = await fetch("http://localhost:5027/api/products");
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}`);
        }
        const data = (await response.json()) as Product[];
        setProducts(data);
      } catch (e: any) {
        setError(e.message ?? "Failed to load products");
      } finally {
        setLoading(false);
      }
    }

    loadProducts();
  }, []);

  if (loading) return <div>Loading products…</div>;
  if (error) return <div>Error: {error}</div>;

  return (
    <div style={{ padding: "1rem" }}>
      <h1>Products</h1>
      {products.length === 0 ? (
        <p>No products found.</p>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>Price</th>
              <th>Active</th>
            </tr>
          </thead>
          <tbody>
            {products.map(p => (
              <tr key={p.id}>
                <td>{p.name}</td>
                <td>{p.unitPrice.toFixed(2)}</td>
                <td>{p.isActive ? "Yes" : "No"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}

export default App;

