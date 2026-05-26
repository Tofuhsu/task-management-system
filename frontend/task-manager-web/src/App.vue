<script setup>
import { ref, onMounted } from "vue";
import api from "./services/api";

const tasks = ref([]);

const newTask = ref({
  title: "",
  description: "",
  isCompleted: false,
  priority: 2,
});

const loadTasks = async () => {
  try {
    const response = await api.get("/tasks");
    tasks.value = response.data;
  } catch (error) {
    console.error(error);
  }
};

const createTask = async () => {
  if (!newTask.value.title.trim()) return;

  try {
    await api.post("/tasks", newTask.value);

    newTask.value = {
      title: "",
      description: "",
      isCompleted: false,
      priority: 2,
    };

    await loadTasks();
  } catch (error) {
    console.error("Create task failed:", error);
    alert("新增失敗，請看 console");
  }
};

const deleteTask = async (id) => {
  try {
    await api.delete(`/tasks/${id}`);
    await loadTasks();
  } catch (error) {
    console.error(error);
  }
};

const toggleComplete = async (task) => {
  try {
    await api.put(`/tasks/${task.id}`, {
      ...task,
      isCompleted: !task.isCompleted,
    });

    await loadTasks();
  } catch (error) {
    console.error(error);
  }
};

onMounted(() => {
  loadTasks();
});
</script>

<template>
  <div class="container">
    <h1>Task Manager</h1>

    <div class="form">
      <input v-model="newTask.title" placeholder="Task title" />

      <textarea
        v-model="newTask.description"
        placeholder="Description"
      ></textarea>

      <button @click="createTask">Add Task</button>
    </div>

    <div class="task-list">
      <div
        v-for="task in tasks"
        :key="task.id"
        class="task-card"
      >
        <div>
          <h3 :class="{ done: task.isCompleted }">
            {{ task.title }}
          </h3>

          <p>{{ task.description }}</p>

          <small>
            Priority: {{ task.priority }}
          </small>
        </div>

        <div class="actions">
          <button @click="toggleComplete(task)">
            {{ task.isCompleted ? "Undo" : "Complete" }}
          </button>

          <button @click="deleteTask(task.id)">
            Delete
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<style>
body {
  font-family: Arial, sans-serif;
  background: #121212;
  color: white;
  margin: 0;
}

.container {
  max-width: 800px;
  margin: 40px auto;
  padding: 20px;
}

.form {
  display: flex;
  flex-direction: column;
  gap: 10px;
  margin-bottom: 30px;
}

input,
textarea {
  padding: 10px;
  border-radius: 8px;
  border: none;
}

button {
  padding: 10px;
  border: none;
  border-radius: 8px;
  cursor: pointer;
}

.task-card {
  background: #1f1f1f;
  padding: 20px;
  border-radius: 12px;
  margin-bottom: 15px;

  display: flex;
  justify-content: space-between;
  align-items: center;
}

.done {
  text-decoration: line-through;
  opacity: 0.6;
}

.actions {
  display: flex;
  gap: 10px;
}
</style>